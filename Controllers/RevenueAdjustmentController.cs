using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.ViewModels;
using RevenueAccountingMVC.Services;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RevenueAccountingMVC.Controllers
{
    public class RevenueAdjustmentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly JournalEntryService _journalService; // Bổ sung Service sinh bút toán

        // Khởi tạo thêm JournalEntryService
        public RevenueAdjustmentController(ApplicationDbContext context, JournalEntryService journalService)
        {
            _context = context;
            _journalService = journalService;
        }

        [Authorize(Roles = "Accountant, Leader")]
        public async Task<IActionResult> Index()
        {
            var data = await _context.RevenueAdjustments
                .Include(r => r.Customer)
                .OrderByDescending(r => r.AccountingDate)
                .ToListAsync();

            return View(data);
        }

        // ===================== GET CREATE =====================
        [HttpGet]
        [Authorize(Roles = "Accountant")]
        public IActionResult Create()
        {
            ViewBag.Customers = new SelectList(_context.Customers, "Id", "CustomerCode");
            ViewBag.Vouchers = new SelectList(Enumerable.Empty<SelectListItem>());
            ViewBag.Accounts = new SelectList(
                _context.Accounts.Where(a => a.IsDetail).ToList(),
                "Id",
                "AccountNumber"
            );

            var model = new RevenueAdjustmentViewModel
            {
                AdjustmentCode = RevenueAdjustment.GenerateAdjustmentCode(
                    _context.RevenueAdjustments.Count() + 1
                ),
                AdjustmentDate = DateTime.Now,
                AccountingDate = DateTime.Now
            };

            return View(model);
        }

        // ===================== POST CREATE =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Create(RevenueAdjustmentViewModel model)
        {
            // ================= VALIDATION =================
            if (model.CustomerId <= 0)
                ModelState.AddModelError("", "Vui lòng chọn khách hàng");

            if (model.OriginalSalesVoucherId <= 0)
                ModelState.AddModelError("", "Vui lòng chọn chứng từ gốc");

            if (model.Details == null || model.Details.Count == 0)
                ModelState.AddModelError("", "Chưa có chi tiết điều chỉnh");

            // KIỂM TRA LỊCH SỬ TRẢ LẠI TRONG DATABASE
            List<RevenueAdjustmentDetail> prevDetails = new List<RevenueAdjustmentDetail>();
            SalesVoucher originalVoucher = null;

            if (model.OriginalSalesVoucherId > 0)
            {
                originalVoucher = await _context.SalesVouchers
                    .Include(v => v.Details)
                    .FirstOrDefaultAsync(v => v.Id == model.OriginalSalesVoucherId);

                // Lấy tất cả các lần điều chỉnh trước đó của chứng từ này
                var previousAdjustments = await _context.RevenueAdjustments
                    .Include(a => a.Details)
                    .Where(a => a.OriginalSalesVoucherId == model.OriginalSalesVoucherId)
                    .ToListAsync();

                prevDetails = previousAdjustments.SelectMany(a => a.Details).ToList();
            }

            if (model.Details != null)
            {
                for (int i = 0; i < model.Details.Count; i++)
                {
                    var d = model.Details[i];

                    if (string.IsNullOrEmpty(d.AdjustmentType))
                    {
                        ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn loại giảm");
                        continue;
                    }

                    // Validate số lượng/đơn giá chống vượt quá mức CÒN LẠI
                    if (originalVoucher != null)
                    {
                        var origLine = originalVoucher.Details.FirstOrDefault(x => x.ProductId == d.ProductId);
                        if (origLine != null)
                        {
                            if (d.AdjustmentType == "TraLai")
                            {
                                if (d.Quantity >= 0)
                                    ModelState.AddModelError("", $"Dòng {i + 1}: Số lượng trả lại phải là số âm");

                                // Tính số lượng ĐÃ trả lại ở các phiếu trước
                                var alreadyReturned = prevDetails
                                    .Where(pd => pd.ProductId == d.ProductId && pd.AdjustmentType == "TraLai")
                                    .Sum(pd => Math.Abs(pd.Quantity));

                                var remainQty = origLine.Quantity - alreadyReturned;

                                if (Math.Abs(d.Quantity) > remainQty)
                                    ModelState.AddModelError("", $"Dòng {i + 1}: Vượt quá số lượng gốc còn lại (Chỉ còn {remainQty})");
                            }

                            if (d.AdjustmentType == "GiamGia")
                            {
                                // Tính đơn giá ĐÃ giảm ở các phiếu trước
                                var alreadyDiscounted = prevDetails
                                    .Where(pd => pd.ProductId == d.ProductId && pd.AdjustmentType == "GiamGia")
                                    .Sum(pd => Math.Abs(pd.UnitPrice));

                                var remainPrice = origLine.UnitPrice - alreadyDiscounted;

                                if (Math.Abs(d.UnitPrice) > remainPrice)
                                    ModelState.AddModelError("", $"Dòng {i + 1}: Giảm giá vượt quá mức cho phép (Tối đa {remainPrice})");
                            }
                        }
                    }

                    if (d.AdjustmentType == "ChietKhau")
                    {
                        if (d.DiscountRate < 0 || d.DiscountRate > 100)
                            ModelState.AddModelError("", $"Dòng {i + 1}: Chiết khấu 0-100%");
                    }

                    if (!d.DebitAccountId.HasValue || !d.CreditAccountId.HasValue)
                        ModelState.AddModelError("", $"Dòng {i + 1}: Chưa chọn tài khoản");
                }
            }

            if (!ModelState.IsValid)
            {
                ReloadViewBag(model);
                return View(model);
            }

            // ================= MAP TAX & CREATE ENTITY =================
            var taxDetailMap = model.TaxDetails?.ToDictionary(t => t.RefIndex, t => t);

            var entity = new RevenueAdjustment
            {
                AdjustmentCode = model.AdjustmentCode ?? "GG-" + DateTime.Now.Ticks,
                AdjustmentDate = model.AdjustmentDate == default ? DateTime.Now : model.AdjustmentDate,
                AccountingDate = model.AccountingDate == default ? DateTime.Now : model.AccountingDate,
                CustomerId = model.CustomerId,
                Description = model.Description ?? "",
                OriginalSalesVoucherId = model.OriginalSalesVoucherId,
                TotalDiscountAmount = model.TotalDiscountAmount,
                TotalTaxAmount = model.TotalTaxAmount,
                TotalPayment = model.TotalPayment,
                // SỬA ĐỔI 1: LƯU TRỰC TIẾP VỚI TRẠNG THÁI POSTED (ĐÃ GHI SỔ)
                Status = VoucherStatus.Posted,

                Details = model.Details.Select((d, idx) =>
                {
                    var taxInfo = (taxDetailMap != null && taxDetailMap.ContainsKey(idx)) ? taxDetailMap[idx] : null;

                    var amount = -Math.Abs(d.Amount);
                    var taxAmount = -Math.Abs(taxInfo?.TaxAmount ?? 0);

                    return new RevenueAdjustmentDetail
                    {
                        ProductId = d.ProductId,
                        AdjustmentType = d.AdjustmentType,
                        Quantity = d.Quantity,
                        UnitPrice = d.UnitPrice,
                        DiscountRate = d.DiscountRate,
                        Amount = amount,
                        DebitAccountId = d.DebitAccountId,
                        CreditAccountId = d.CreditAccountId,
                        TaxRateSnapshot = d.OriginalTaxRate,
                        TaxAmount = taxAmount,
                        TaxAccountId = taxInfo?.TaxAccountId
                    };
                }).ToList()
            };

            try
            {
                _context.Add(entity);
                await _context.SaveChangesAsync(); // Lưu chứng từ vào DB

                // SỬA ĐỔI 2: TỰ ĐỘNG GỌI HÀM SINH BÚT TOÁN NGAY SAU KHI LƯU
                await _journalService.GenerateEntriesFromRevenueAdjustmentAsync(entity.Id);

                TempData["SuccessMessage"] = "Lưu và ghi sổ chứng từ thành công!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi khi lưu/ghi sổ: " + ex.Message);
                ReloadViewBag(model);
                return View(model);
            }
        }

        // ===================== SUPPORT =====================
        private void ReloadViewBag(RevenueAdjustmentViewModel model)
        {
            ViewBag.Customers = new SelectList(_context.Customers, "Id", "CustomerCode", model.CustomerId);
            ViewBag.Vouchers = new SelectList(_context.SalesVouchers.Where(x => x.CustomerId == model.CustomerId), "Id", "VoucherCode", model.OriginalSalesVoucherId);
            ViewBag.Accounts = new SelectList(_context.Accounts.Where(a => a.IsDetail).ToList(), "Id", "AccountNumber");
        }

        // ===================== API =====================
        [HttpGet]
        public async Task<IActionResult> GetVouchersByCustomer(int customerId)
        {
            var data = await _context.SalesVouchers
                .Where(x => x.CustomerId == customerId && x.Status == VoucherStatus.Posted)
                .Select(x => new { x.Id, x.VoucherCode })
                .ToListAsync();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetOriginalVoucher(int id)
        {
            var v = await _context.SalesVouchers
                .Include(x => x.Customer)
                .Include(x => x.Details)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (v == null) return NotFound();

            // Tính toán trừ đi phần đã bị điều chỉnh trước đó
            var previousAdjustments = await _context.RevenueAdjustments
                .Include(a => a.Details)
                .Where(a => a.OriginalSalesVoucherId == id)
                .ToListAsync();

            var prevDetails = previousAdjustments.SelectMany(a => a.Details).ToList();

            var details = v.Details.Select(d =>
            {
                var alreadyReturned = prevDetails
                    .Where(pd => pd.ProductId == d.ProductId && pd.AdjustmentType == "TraLai")
                    .Sum(pd => Math.Abs(pd.Quantity));

                var alreadyDiscounted = prevDetails
                    .Where(pd => pd.ProductId == d.ProductId && pd.AdjustmentType == "GiamGia")
                    .Sum(pd => Math.Abs(pd.UnitPrice));

                return new
                {
                    productId = d.ProductId,
                    productCode = d.Product.ProductCode,
                    productName = d.Product.ProductName,
                    // Trả về số lượng/đơn giá CÒN LẠI thay vì gốc
                    originalQty = d.Quantity - alreadyReturned,
                    originalPrice = d.UnitPrice - alreadyDiscounted,
                    originalTaxRate = d.TaxRateSnapshot
                };
            }).Where(d => d.originalQty > 0 || d.originalPrice > 0).ToList(); // Ẩn luôn sản phẩm đã trả sạch

            return Json(new
            {
                customerName = v.Customer.CustomerName,
                customerCode = v.Customer.CustomerCode,
                address = v.Customer.Address,
                voucherDate = v.AccountingDate.ToString("yyyy-MM-dd"),
                totalAmount = v.TotalAmount,
                totalTax = v.TotalTaxAmount,
                totalPayment = v.TotalPayment,
                details = details
            });
        }

        // ===================== API =====================
        [HttpGet]
        public async Task<IActionResult> GetCustomerInfo(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            return Json(new { 
                customerName = customer.CustomerName, 
                address = customer.Address,
                customerCode = customer.CustomerCode
            });
        }
        
    }
}
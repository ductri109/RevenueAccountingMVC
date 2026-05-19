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

        // =======================
        // INDEX - Danh sách
        // =======================
        [Authorize(Roles = "Accountant, Leader")]
        public async Task<IActionResult> Index(string searchString, int pageNumber = 1)
        {
            var query = _context.RevenueAdjustments
                .Include(r => r.Customer)
                .AsQueryable();

            // 1. Lọc theo từ khóa tìm kiếm (Số CT, Mã KH, Tên KH)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(r =>
                    (r.AdjustmentCode != null && r.AdjustmentCode.Contains(searchString)) ||
                    (r.Customer != null && r.Customer.CustomerCode != null && r.Customer.CustomerCode.Contains(searchString)) ||
                    (r.Customer != null && r.Customer.CustomerName != null && r.Customer.CustomerName.Contains(searchString)));
            }

            // 2. Tính tổng thanh toán của TẤT CẢ bản ghi (đã lọc) trước khi phân trang
            decimal totalAmount = await query.SumAsync(r => r.TotalPayment);

            // 3. Tính toán phân trang
            int pageSize = 10;
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var paginatedData = await query
                .OrderByDescending(r => r.AccountingDate)
                .ThenByDescending(r => r.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 4. Lưu lại trạng thái filter, tổng tiền và phân trang
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalItems"] = totalItems;
            ViewData["TotalAmount"] = totalAmount;

            return View(paginatedData);
        }

        // ===================== GET CREATE =====================
        [HttpGet]
        [Authorize(Roles = "Accountant")]
        public IActionResult Create()
        {
            ViewBag.Customers = new SelectList(_context.Customers, "Id", "CustomerCode");
            ViewBag.Vouchers = new SelectList(Enumerable.Empty<SelectListItem>());
            // Bỏ Where(a => a.IsDetail) và sắp xếp theo số tài khoản
            ViewBag.Accounts = new SelectList(
                _context.Accounts.OrderBy(a => a.AccountNumber).ToList(),
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
                // 🔥 ĐẢM BẢO TRẠNG THÁI POSTED (ĐÃ GHI SỔ) ĐỂ SINH BÚT TOÁN
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
                // 🔥 BƯỚC 1: THÊM CHỨNG TỪ VÀO DB
                _context.Add(entity);
                await _context.SaveChangesAsync();

                // 🔥 BƯỚC 2: TỰ ĐỘNG SINH BÚT TOÁN NGAY SAU KHI LƯU
                await _journalService.GenerateEntriesFromRevenueAdjustmentAsync(entity.Id);

                TempData["SuccessMessage"] = $"✓ Lưu và ghi sổ chứng từ {entity.AdjustmentCode} thành công! Bút toán đã được tạo.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"❌ Lỗi khi lưu/ghi sổ: {ex.Message}");
                ReloadViewBag(model);
                return View(model);
            }
        }

        // ===================== SUPPORT =====================
        private void ReloadViewBag(RevenueAdjustmentViewModel model)
        {
            ViewBag.Customers = new SelectList(_context.Customers, "Id", "CustomerCode", model.CustomerId);
            ViewBag.Vouchers = new SelectList(_context.SalesVouchers.Where(x => x.CustomerId == model.CustomerId), "Id", "VoucherCode", model.OriginalSalesVoucherId);
            ViewBag.Accounts = new SelectList(_context.Accounts.OrderBy(a => a.AccountNumber).ToList(), "Id", "AccountNumber");        
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

            return Json(new
            {
                customerName = customer.CustomerName,
                address = customer.Address,
                customerCode = customer.CustomerCode
            });
        }

        // ===================== DETAILS =====================
        [HttpGet]
        [Authorize(Roles = "Accountant, Leader")]
        public async Task<IActionResult> Details(int id)
        {
            var entity = await _context.RevenueAdjustments
                .Include(r => r.Customer)
                .Include(r => r.OriginalSalesVoucher)
                .Include(r => r.Details)
                .ThenInclude(d => d.Product)
                .Include(r => r.Details)
                .ThenInclude(d => d.DebitAccount)
                .Include(r => r.Details)
                .ThenInclude(d => d.CreditAccount)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null) return NotFound();

            // Lấy danh sách bút toán liên quan để hiển thị
            var journalEntries = await _context.JournalEntries
                .Where(j => j.VoucherId == id && j.VoucherType == "RevenueAdjustment")
                .Include(j => j.Account)
                .OrderBy(j => j.EntryType)
                .ToListAsync();

            var model = new RevenueAdjustmentViewModel
            {
                AdjustmentCode = entity.AdjustmentCode,
                AdjustmentDate = entity.AdjustmentDate,
                AccountingDate = entity.AccountingDate,
                CustomerId = entity.CustomerId,
                CustomerName = entity.Customer?.CustomerName,
                CustomerCode = entity.Customer?.CustomerCode,
                Address = entity.Customer?.Address,
                Description = entity.Description,
                OriginalSalesVoucherId = entity.OriginalSalesVoucherId,
                OriginalVoucherDate = entity.OriginalSalesVoucher?.AccountingDate,
                OriginalTotalAmount = entity.OriginalSalesVoucher?.TotalAmount ?? 0,
                OriginalTotalTax = entity.OriginalSalesVoucher?.TotalTaxAmount ?? 0,
                OriginalTotalPayment = entity.OriginalSalesVoucher?.TotalPayment ?? 0,
                TotalDiscountAmount = entity.TotalDiscountAmount,
                TotalTaxAmount = entity.TotalTaxAmount,
                TotalPayment = entity.TotalPayment,
                Status = entity.Status.ToString(),
                Details = entity.Details.Select(d => new RevenueAdjustmentDetailVM
                {
                    ProductId = d.ProductId,
                    ProductCode = d.Product?.ProductCode,
                    ProductName = d.Product?.ProductName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    DiscountRate = d.DiscountRate,
                    Amount = d.Amount,
                    AdjustmentType = d.AdjustmentType,
                    DebitAccountId = d.DebitAccountId,
                    DebitAccountNumber = d.DebitAccount?.AccountNumber,
                    CreditAccountId = d.CreditAccountId,
                    CreditAccountNumber = d.CreditAccount?.AccountNumber,
                    OriginalQty = d.Quantity,
                    OriginalPrice = d.UnitPrice,
                    OriginalTaxRate = d.TaxRateSnapshot
                }).ToList(),
                TaxDetails = entity.Details.Select((d, idx) => new RevenueAdjustmentTaxVM
                {
                    ProductId = d.ProductId,
                    RefIndex = idx,
                    TaxRate = d.TaxRateSnapshot,
                    TaxAmount = d.TaxAmount
                }).ToList()
            };

            // Lưu danh sách bút toán vào ViewBag để hiển thị trên view
            ViewBag.JournalEntries = journalEntries;
            ViewBag.JournalEntryCount = journalEntries.Count;

            return View(model);
        }

        // ===================== GET EDIT =====================
        [HttpGet]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Edit(int id)
        {
            var entity = await _context.RevenueAdjustments
                .Include(r => r.Customer)
                .Include(r => r.OriginalSalesVoucher)
                .ThenInclude(v => v.Details)
                .Include(r => r.Details)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null) return NotFound();

            var model = new RevenueAdjustmentViewModel
            {
                AdjustmentCode = entity.AdjustmentCode,
                AdjustmentDate = entity.AdjustmentDate,
                AccountingDate = entity.AccountingDate,
                CustomerId = entity.CustomerId,
                CustomerName = entity.Customer?.CustomerName,
                CustomerCode = entity.Customer?.CustomerCode,
                Address = entity.Customer?.Address,
                Description = entity.Description,
                OriginalSalesVoucherId = entity.OriginalSalesVoucherId,
                OriginalVoucherDate = entity.OriginalSalesVoucher?.AccountingDate,
                OriginalTotalAmount = entity.OriginalSalesVoucher?.TotalAmount ?? 0,
                OriginalTotalTax = entity.OriginalSalesVoucher?.TotalTaxAmount ?? 0,
                OriginalTotalPayment = entity.OriginalSalesVoucher?.TotalPayment ?? 0,
                TotalDiscountAmount = entity.TotalDiscountAmount,
                TotalTaxAmount = entity.TotalTaxAmount,
                TotalPayment = entity.TotalPayment,
                Details = entity.Details.Select((d, idx) => new RevenueAdjustmentDetailVM
                {
                    ProductId = d.ProductId,
                    ProductName = d.Product?.ProductName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    DiscountRate = d.DiscountRate,
                    Amount = d.Amount,
                    AdjustmentType = d.AdjustmentType,
                    DebitAccountId = d.DebitAccountId,
                    CreditAccountId = d.CreditAccountId,
                    OriginalQty = d.Quantity,
                    OriginalPrice = d.UnitPrice,
                    OriginalTaxRate = d.TaxRateSnapshot
                }).ToList(),
                TaxDetails = entity.Details.Select((d, idx) => new RevenueAdjustmentTaxVM
                {
                    ProductId = d.ProductId,
                    RefIndex = idx,
                    TaxRate = d.TaxRateSnapshot,
                    TaxAmount = d.TaxAmount,
                    TaxAccountId = d.TaxAccountId
                }).ToList()
            };

            ReloadViewBag(model);
            return View(model);
        }

        // ===================== POST EDIT =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Edit(int id, RevenueAdjustmentViewModel model)
        {
            // ================= VALIDATION =================
            if (model.CustomerId <= 0)
                ModelState.AddModelError("", "Vui lòng chọn khách hàng");

            if (model.OriginalSalesVoucherId <= 0)
                ModelState.AddModelError("", "Vui lòng chọn chứng từ gốc");

            if (model.Details == null || model.Details.Count == 0)
                ModelState.AddModelError("", "Chưa có chi tiết điều chỉnh");

            List<RevenueAdjustmentDetail> prevDetails = new List<RevenueAdjustmentDetail>();
            SalesVoucher originalVoucher = null;

            if (model.OriginalSalesVoucherId > 0)
            {
                originalVoucher = await _context.SalesVouchers
                    .Include(v => v.Details)
                    .FirstOrDefaultAsync(v => v.Id == model.OriginalSalesVoucherId);

                var previousAdjustments = await _context.RevenueAdjustments
                    .Include(a => a.Details)
                    .Where(a => a.OriginalSalesVoucherId == model.OriginalSalesVoucherId && a.Id != id)
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

                    if (originalVoucher != null)
                    {
                        var origLine = originalVoucher.Details.FirstOrDefault(x => x.ProductId == d.ProductId);
                        if (origLine != null)
                        {
                            if (d.AdjustmentType == "TraLai")
                            {
                                if (d.Quantity >= 0)
                                    ModelState.AddModelError("", $"Dòng {i + 1}: Số lượng trả lại phải là số âm");

                                var alreadyReturned = prevDetails
                                    .Where(pd => pd.ProductId == d.ProductId && pd.AdjustmentType == "TraLai")
                                    .Sum(pd => Math.Abs(pd.Quantity));

                                var remainQty = origLine.Quantity - alreadyReturned;

                                if (Math.Abs(d.Quantity) > remainQty)
                                    ModelState.AddModelError("", $"Dòng {i + 1}: Vượt quá số lượng gốc còn lại (Chỉ còn {remainQty})");
                            }

                            if (d.AdjustmentType == "GiamGia")
                            {
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

            var entity = await _context.RevenueAdjustments
                .Include(r => r.Details)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null) return NotFound();

            try
            {
                // 🔥 BƯỚC 1: XÓA TẤT CẢ BÚT TOÁN CŨ TRƯỚC KHI CẬP NHẬT
                var oldJournalEntries = await _context.JournalEntries
                    .Where(j => j.VoucherId == entity.Id && j.VoucherType == "RevenueAdjustment")
                    .ToListAsync();
                if (oldJournalEntries.Count > 0)
                {
                    _context.JournalEntries.RemoveRange(oldJournalEntries);
                    await _context.SaveChangesAsync();
                }

                // 🔥 BƯỚC 2: CẬP NHẬT THÔNG TIN CHỨNG TỪ
                var taxDetailMap = model.TaxDetails?.ToDictionary(t => t.RefIndex, t => t);

                entity.AdjustmentDate = model.AdjustmentDate;
                entity.AccountingDate = model.AccountingDate;
                entity.CustomerId = model.CustomerId;
                entity.Description = model.Description ?? "";
                entity.OriginalSalesVoucherId = model.OriginalSalesVoucherId;
                entity.TotalDiscountAmount = model.TotalDiscountAmount;
                entity.TotalTaxAmount = model.TotalTaxAmount;
                entity.TotalPayment = model.TotalPayment;
                entity.Status = VoucherStatus.Posted; // Đảm bảo status là Posted

                // 🔥 BƯỚC 3: CẬP NHẬT CHI TIẾT (XÓA CŨ, THÊM MỚI)
                _context.RevenueAdjustmentDetails.RemoveRange(entity.Details);
                await _context.SaveChangesAsync();

                entity.Details = model.Details.Select((d, idx) =>
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
                }).ToList();

                // 🔥 BƯỚC 4: LƯU CẬP NHẬT CHỨNG TỪ
                _context.Update(entity);
                await _context.SaveChangesAsync();

                // 🔥 BƯỚC 5: TỰ ĐỘNG SINH BÚT TOÁN MỚI
                await _journalService.GenerateEntriesFromRevenueAdjustmentAsync(entity.Id);

                TempData["SuccessMessage"] = "✓ Cập nhật và ghi sổ chứng từ thành công! Bút toán đã được tạo.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"❌ Lỗi khi cập nhật/ghi sổ: {ex.Message}");
                ReloadViewBag(model);
                return View(model);
            }
        }

        // ===================== DELETE =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.RevenueAdjustments
                .Include(r => r.Details)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null) return NotFound();

            try
            {
                // 🔥 BƯỚC 1: XÓA TẤT CẢ BÚT TOÁN LIÊN QUAN
                var journalEntries = await _context.JournalEntries
                    .Where(j => j.VoucherId == entity.Id && j.VoucherType == "RevenueAdjustment")
                    .ToListAsync();

                if (journalEntries.Count > 0)
                {
                    _context.JournalEntries.RemoveRange(journalEntries);
                    await _context.SaveChangesAsync();
                }

                // 🔥 BƯỚC 2: XÓA CHI TIẾT CHỨNG TỪ
                if (entity.Details.Count > 0)
                {
                    _context.RevenueAdjustmentDetails.RemoveRange(entity.Details);
                    await _context.SaveChangesAsync();
                }

                // 🔥 BƯỚC 3: XÓA CHỨNG TỪ CHÍNH
                _context.RevenueAdjustments.Remove(entity);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"✓ Xóa chứng từ {entity.AdjustmentCode} thành công! ({journalEntries.Count} bút toán đã được xóa)";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ Lỗi khi xóa: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RevenueAccountingMVC.Controllers
{
    public class RevenueAdjustmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RevenueAdjustmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var data = await _context.RevenueAdjustments
                .Include(r => r.Customer)
                .OrderByDescending(r => r.AccountingDate)
                .ToListAsync();

            return View(data);
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Customers = new SelectList(_context.Customers, "Id", "CustomerName");

            // FIX: tránh null dropdown
            ViewBag.Vouchers = new SelectList(Enumerable.Empty<SelectListItem>());

            var model = new RevenueAdjustmentViewModel
            {
                AdjustmentCode = RevenueAdjustment.GenerateAdjustmentCode(_context.RevenueAdjustments.Count() + 1),
                AdjustmentDate = DateTime.Now,
                AccountingDate = DateTime.Now
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RevenueAdjustmentViewModel model)
        {
            // ===== BASIC =====
            if (model.CustomerId == 0)
                ModelState.AddModelError("", "Vui lòng chọn khách hàng.");

            if (model.OriginalSalesVoucherId == 0)
                ModelState.AddModelError("", "Vui lòng chọn chứng từ gốc.");

            // ===== FILTER DETAILS =====
            if (model.Details != null)
            {
                model.Details = model.Details
                    .Where(x => !string.IsNullOrEmpty(x.AdjustmentType))
                    .ToList();
            }

            if (model.Details == null || !model.Details.Any())
                ModelState.AddModelError("", "Phải có ít nhất 1 dòng điều chỉnh.");

            // ===== LOAD CT GỐC =====
            var originalVoucher = await _context.SalesVouchers
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == model.OriginalSalesVoucherId);

            if (originalVoucher == null)
            {
                ModelState.AddModelError("", "Không tìm thấy chứng từ gốc.");
            }
            else
            {
                if (originalVoucher.CustomerId != model.CustomerId)
                    ModelState.AddModelError("", "Khách hàng không khớp.");

                if (model.Details != null)
                {
                    foreach (var d in model.Details)
                    {
                        var original = originalVoucher.Details
                            .FirstOrDefault(x => x.ProductId == d.ProductId);

                        if (original == null)
                        {
                            ModelState.AddModelError("", $"SP {d.ProductName} không tồn tại.");
                            continue;
                        }

                        // ===== VALIDATE THEO TYPE =====
                        if (d.AdjustmentType == "TraLai")
                        {
                            if (d.Quantity <= 0)
                                ModelState.AddModelError("", $"[{d.ProductName}] SL phải > 0");

                            if (d.Quantity > original.Quantity)
                                ModelState.AddModelError("", $"[{d.ProductName}] vượt SL gốc");
                        }
                        else if (d.AdjustmentType == "GiamGia")
                        {
                            if (d.UnitPrice <= 0)
                                ModelState.AddModelError("", $"[{d.ProductName}] giá phải > 0");

                            if (d.UnitPrice > original.UnitPrice)
                                ModelState.AddModelError("", $"[{d.ProductName}] vượt giá gốc");
                        }
                        else if (d.AdjustmentType == "ChietKhau")
                        {
                            if (d.DiscountRate <= 0 || d.DiscountRate > 100)
                                ModelState.AddModelError("", $"[{d.ProductName}] % không hợp lệ");
                        }

                        // ===== FIX QUAN TRỌNG =====
                        if (!string.IsNullOrEmpty(d.AdjustmentType))
                        {
                            if (d.Amount >= 0)
                                ModelState.AddModelError("", $"[{d.ProductName}] Thành tiền phải âm");
                        }
                    }

                    // ===== CHECK TOTAL =====
                    if (model.TotalDiscountAmount == 0)
                    {
                        ModelState.AddModelError("", "Tổng tiền điều chỉnh phải khác 0.");
                    }

                    var totalAdjustedBefore = _context.RevenueAdjustments
                        .Where(x => x.OriginalSalesVoucherId == model.OriginalSalesVoucherId)
                        .Sum(x => (decimal?)x.TotalDiscountAmount) ?? 0;

                    if (Math.Abs(totalAdjustedBefore + model.TotalDiscountAmount) > originalVoucher.TotalAmount)
                    {
                        ModelState.AddModelError("", "Vượt giá trị chứng từ gốc.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Customers = new SelectList(_context.Customers, "Id", "CustomerName", model.CustomerId);

                ViewBag.Vouchers = new SelectList(
                    _context.SalesVouchers.Where(x => x.CustomerId == model.CustomerId),
                    "Id", "VoucherCode", model.OriginalSalesVoucherId
                );

                return View(model);
            }

            // ===== SAVE =====
            var entity = new RevenueAdjustment
            {
                AdjustmentCode = model.AdjustmentCode,
                AdjustmentDate = model.AdjustmentDate,
                AccountingDate = model.AccountingDate,
                CustomerId = model.CustomerId,
                Description = model.Description,
                OriginalSalesVoucherId = model.OriginalSalesVoucherId,
                TotalDiscountAmount = model.TotalDiscountAmount,
                TotalTaxAmount = model.TotalTaxAmount,
                TotalPayment = model.TotalPayment,
                Status = VoucherStatus.Draft,
                Details = model.Details.Select(d => new RevenueAdjustmentDetail
                {
                    ProductId = d.ProductId,
                    AdjustmentType = d.AdjustmentType,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    DiscountRate = d.DiscountRate,
                    Amount = d.Amount,
                    TaxRateSnapshot = d.OriginalTaxRate,
                    TaxAmount = Math.Round(d.Amount * (d.OriginalTaxRate / 100m), 2)
                }).ToList()
            };

            _context.Add(entity);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ===== API =====
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

            return Json(new
            {
                customerName = v.Customer.CustomerName,
                customerCode = v.Customer.CustomerCode,
                address = v.Customer.Address,
                voucherDate = v.AccountingDate.ToString("yyyy-MM-dd"),
                totalAmount = v.TotalAmount,
                totalTax = v.TotalTaxAmount,
                totalPayment = v.TotalPayment,
                details = v.Details.Select(d => new
                {
                    productId = d.ProductId,
                    productName = d.Product.ProductName,
                    originalQty = d.Quantity,
                    originalPrice = d.UnitPrice,
                    originalTaxRate = d.TaxRateSnapshot
                })
            });
        }
    }
}
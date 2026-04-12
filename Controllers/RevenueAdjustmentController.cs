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

        // UC3: Màn hình danh sách
        public async Task<IActionResult> Index()
        {
            var data = await _context.RevenueAdjustments
                .Include(r => r.Customer)
                .OrderByDescending(r => r.AccountingDate)
                .ToListAsync();
            return View(data);
        }

        // UC3.1: Giao diện Ghi giảm
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Load danh sách chứng từ đã ghi sổ để chọn
            var postedVouchers = await _context.SalesVouchers
                .Where(v => v.Status == VoucherStatus.Posted)
                .Select(v => new { v.Id, v.VoucherCode })
                .ToListAsync();
                
            ViewBag.SalesVouchers = new SelectList(postedVouchers, "Id", "VoucherCode");

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
        public async Task<IActionResult> Create(RevenueAdjustmentViewModel model, string actionType)
        {
            if (ModelState.IsValid)
            {
                // Validate Backend
                if (model.Details.Any(x => x.Amount >= 0))
                {
                    ModelState.AddModelError("", "Tất cả các dòng ghi giảm phải có thành tiền âm.");
                }
                
                if (ModelState.ErrorCount == 0)
                {
                    var entity = new RevenueAdjustment
                    {
                        AdjustmentCode = model.AdjustmentCode ?? RevenueAdjustment.GenerateAdjustmentCode(_context.RevenueAdjustments.Count() + 1),
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
                            TaxAmount = d.Amount * (d.OriginalTaxRate / 100m) // Tính thuế
                        }).ToList()
                    };

                    _context.RevenueAdjustments.Add(entity);
                    await _context.SaveChangesAsync();

                    if (actionType == "SaveAndNew")
                        return RedirectToAction(nameof(Create));

                    return RedirectToAction(nameof(Index));
                }
            }

            // Nếu lỗi, load lại dropdown
            var postedVouchers = await _context.SalesVouchers.Where(v => v.Status == VoucherStatus.Posted).ToListAsync();
            ViewBag.SalesVouchers = new SelectList(postedVouchers, "Id", "VoucherCode", model.OriginalSalesVoucherId);
            return View(model);
        }

        // API gọi bằng AJAX để load dữ liệu CT gốc
        [HttpGet]
        public async Task<IActionResult> GetOriginalVoucher(int id)
        {
            var voucher = await _context.SalesVouchers
                .Include(v => v.Customer)
                .Include(v => v.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null) return NotFound();

            var result = new
            {
                customerId = voucher.CustomerId,
                customerName = voucher.Customer!.CustomerName,
                customerCode = voucher.Customer.CustomerCode,
                address = voucher.Customer.Address,
                taxCode = voucher.Customer.TaxCode,
                voucherDate = voucher.AccountingDate.ToString("yyyy-MM-dd"),
                totalAmount = voucher.TotalAmount,
                totalTax = voucher.TotalTaxAmount,
                totalPayment = voucher.TotalPayment,
                details = voucher.Details.Select(d => new {
                    productId = d.ProductId,
                    productName = d.Product?.ProductName ?? d.ProductNameSnapshot,
                    originalQty = d.Quantity,
                    originalPrice = d.UnitPrice,
                    originalTaxRate = d.TaxRateSnapshot
                })
            };

            return Json(result);
        }
    }
}
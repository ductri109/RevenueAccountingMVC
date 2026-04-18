using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;

namespace RevenueAccountingMVC.Controllers
{
    public class SalesVoucherController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SalesVoucherController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================
        // 1. INDEX - Danh sách
        // =======================
        public async Task<IActionResult> Index(string searchString)
        {
            var vouchers = _context.SalesVouchers
                .Include(v => v.Customer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                vouchers = vouchers.Where(v =>
                    v.VoucherCode.Contains(searchString) ||
                    (v.CustomerNameSnapshot != null && v.CustomerNameSnapshot.Contains(searchString)));
            }

            ViewData["CurrentSearch"] = searchString;

            return View(await vouchers
                .OrderByDescending(v => v.AccountingDate)
                .ThenByDescending(v => v.Id)
                .ToListAsync());
        }

        // =======================
        // 2. CREATE (GET)
        // =======================
        public async Task<IActionResult> Create()
        {
            await LoadDropdownData();

            int next = await _context.SalesVouchers.CountAsync() + 1;
            ViewBag.NextCode = SalesVoucher.GenerateVoucherCode(next);

            var model = new SalesVoucher();
            model.Details.Add(new SalesVoucherDetail());
            model.AccountingDate = DateTime.Now.Date;

            return View(model);
        }

        // =======================
        // 3. CREATE (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesVoucher model)
        {
            if (model.Details != null)
            {
                model.Details.RemoveAll(d => d.ProductId == 0);
            }

            if (model.Details == null || model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 hàng hóa/dịch vụ hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownData();
                ViewBag.NextCode = model.VoucherCode ?? SalesVoucher.GenerateVoucherCode(await _context.SalesVouchers.CountAsync() + 1);
                return View(model);
            }

            int next = await _context.SalesVouchers.CountAsync() + 1;
            model.VoucherCode = SalesVoucher.GenerateVoucherCode(next);

            model.TotalAmount = 0;
            model.TotalTaxAmount = 0;

            var customer = await _context.Customers.FindAsync(model.CustomerId);
            if (customer != null)
            {
                model.CustomerNameSnapshot = customer.CustomerName;
                model.CustomerAddressSnapshot = customer.Address;
            }

            model.DueDate = model.AccountingDate.AddDays(model.DebtDays);
            model.CreatedAt = DateTime.Now;
            model.Status = VoucherStatus.Draft;

            foreach (var detail in model.Details)
            {
                var product = await _context.Products.FindAsync(detail.ProductId);
                if (product != null)
                {
                    detail.ProductNameSnapshot = product.ProductName;
                    detail.UnitSnapshot = product.Unit;
                }

                detail.Amount = detail.Quantity * detail.UnitPrice * (1 - (detail.DiscountRate / 100m));
                detail.TaxAmount = detail.Amount * (detail.TaxRateSnapshot / 100m);

                model.TotalAmount += detail.Amount;
                model.TotalTaxAmount += detail.TaxAmount;
            }

            model.TotalPayment = model.TotalAmount + model.TotalTaxAmount;

            _context.SalesVouchers.Add(model);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Ghi nhận doanh thu thành công (Số CT: {model.VoucherCode})";
            return RedirectToAction(nameof(Index));
        }

        // =======================
        // 4. DELETE
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var voucher = await _context.SalesVouchers
                .Include(v => v.Details)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null)
                return Json(new { success = false, message = "Không tìm thấy chứng từ." });

            if (voucher.Status == VoucherStatus.Posted)
            {
                return Json(new { success = false, message = "Không thể xóa chứng từ đã ghi sổ!" });
            }

            _context.SalesVoucherDetails.RemoveRange(voucher.Details);
            _context.SalesVouchers.Remove(voucher);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // =======================
        // 5. DETAILS
        // =======================
        public async Task<IActionResult> Details(int id)
        {
            var voucher = await _context.SalesVouchers
                .Include(v => v.Customer)
                .Include(v => v.Details)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null) return NotFound();

            return View(voucher);
        }

        // =======================
        // 6. EDIT (GET)
        // =======================
        public async Task<IActionResult> Edit(int id)
        {
            var voucher = await _context.SalesVouchers
                .Include(v => v.Details)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null) return NotFound();

            if (voucher.Status == VoucherStatus.Posted)
            {
                TempData["ErrorMessage"] = "Chứng từ đã ghi sổ, không thể chỉnh sửa!";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownData();
            return View(voucher);
        }

        // =======================
        // 7. EDIT (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SalesVoucher model)
        {
            if (id != model.Id) return NotFound();

            if (model.Details != null)
            {
                model.Details.RemoveAll(d => d.ProductId == 0);
            }

            if (model.Details == null || model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 hàng hóa hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownData();
                return View(model);
            }

            var existing = await _context.SalesVouchers
                .Include(v => v.Details)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (existing == null) return NotFound();

            if (existing.Status == VoucherStatus.Posted)
            {
                TempData["ErrorMessage"] = "Không thể sửa chứng từ đã ghi sổ!";
                return RedirectToAction(nameof(Index));
            }

            // Xóa detail cũ
            _context.SalesVoucherDetails.RemoveRange(existing.Details);

            // Update header
            existing.AccountingDate = model.AccountingDate;
            existing.CustomerId = model.CustomerId;

            var customer = await _context.Customers.FindAsync(model.CustomerId);
            if (customer != null)
            {
                existing.CustomerNameSnapshot = customer.CustomerName;
                existing.CustomerAddressSnapshot = customer.Address;
            }

            existing.Description = model.Description;
            existing.DebtDays = model.DebtDays;
            existing.DueDate = model.AccountingDate.AddDays(model.DebtDays);

            existing.TotalAmount = 0;
            existing.TotalTaxAmount = 0;

            // Clone detail chuẩn EF
            existing.Details = model.Details.Select(d => new SalesVoucherDetail
            {
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                DiscountRate = d.DiscountRate,
                TaxRateSnapshot = d.TaxRateSnapshot
            }).ToList();

            foreach (var detail in existing.Details)
            {
                var product = await _context.Products.FindAsync(detail.ProductId);
                if (product != null)
                {
                    detail.ProductNameSnapshot = product.ProductName;
                    detail.UnitSnapshot = product.Unit;
                }

                detail.Amount = detail.Quantity * detail.UnitPrice * (1 - detail.DiscountRate / 100m);
                detail.TaxAmount = detail.Amount * (detail.TaxRateSnapshot / 100m);

                existing.TotalAmount += detail.Amount;
                existing.TotalTaxAmount += detail.TaxAmount;
            }

            existing.TotalPayment = existing.TotalAmount + existing.TotalTaxAmount;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }

        // =======================
        // HELPER DROPDOWNS
        // =======================
        private async Task LoadDropdownData()
        {
            var customers = await _context.Customers
                .Where(c => c.Status == CustomerStatus.Active)
                .OrderBy(c => c.CustomerCode)
                .ToListAsync();

            ViewBag.Customers = new SelectList(customers, "Id", "CustomerCode");

            ViewBag.CustomersData = customers.ToDictionary(
                c => c.Id,
                c => new { c.CustomerName, c.Address, c.MaxDebtDays, c.ReceivableAccountId }
            );

            var products = await _context.Products
                .Include(p => p.Tax)
                .Where(p => p.Status == ProductStatus.Active)
                .ToListAsync();

            ViewBag.Products = new SelectList(products, "Id", "ProductCode");

            ViewBag.ProductsData = products.ToDictionary(
                p => p.Id,
                p => new
                {
                    p.ProductName,
                    p.Unit,
                    p.DefaultUnitPrice,
                    p.RevenueAccountId,
                    p.TaxId,
                    TaxAccountId = p.Tax?.TaxAccountId
                }
            );

            ViewBag.Accounts = new SelectList(
                await _context.Accounts.Where(a => a.IsDetail).ToListAsync(),
                "Id",
                "AccountNumber"
            );

            ViewBag.Taxes = new SelectList(
                await _context.Taxes.Where(t => t.Status == TaxStatus.Active).ToListAsync(),
                "Id",
                "TaxName"
            );
        }

        // =======================
        // API AUTO FILL
        // =======================
        [HttpGet]
        public async Task<IActionResult> GetCustomerDefaults(int id)
        {
            var c = await _context.Customers.FindAsync(id);
            if (c == null) return NotFound();

            return Json(new
            {
                customerName = c.CustomerName,
                address = c.Address,
                maxDebtDays = c.MaxDebtDays ?? 0,
                receivableAccountId = c.ReceivableAccountId
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDefaults(int id)
        {
            var p = await _context.Products
                .Include(x => x.Tax)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            return Json(new
            {
                productName = p.ProductName,
                unit = p.Unit,
                defaultPrice = p.DefaultUnitPrice ?? 0,
                revenueAccountId = p.RevenueAccountId,
                taxId = p.TaxId,
                taxRate = p.DefaultTaxRate ?? (p.Tax != null ? p.Tax.TaxRate : 0),
                taxAccountId = p.Tax != null ? (int?)p.Tax.TaxAccountId : null
            });
        }

        [HttpPost]
        public async Task<IActionResult> PostVoucher(int id)
        {
            var voucher = await _context.SalesVouchers.FindAsync(id);
            if (voucher == null) return NotFound();

            if (voucher.Status == VoucherStatus.Posted)
            {
                return Json(new { success = false, message = "Chứng từ đã ghi sổ rồi!" });
            }

            voucher.Status = VoucherStatus.Posted;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.Services;

namespace RevenueAccountingMVC.Controllers
{
    public class SalesVoucherController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly JournalEntryService _journalService;

        public SalesVoucherController(ApplicationDbContext context, JournalEntryService journalService)
        {
            _context = context;
            _journalService = journalService;
        }

        // =======================
        // 1. INDEX - Danh sách
        // =======================
        public async Task<IActionResult> Index(string searchString)
        {
            var vouchers = _context.SalesVouchers
                .Include(v => v.Customer)
                // THÊM DÒNG NÀY: Chỉ hiển thị các chứng từ có Status là Posted
                .Where(v => v.Status == VoucherStatus.Posted)
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
            model.VoucherCode = SalesVoucher.GenerateVoucherCode(next); 
            model.Details.Add(new SalesVoucherDetail());
            model.AccountingDate = DateTime.Now.Date;

            return View(model);
        }

        // =======================
        // 3. CREATE (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalesVoucher model, string submitAction)
        {
            if (model.Details != null)
            {
                model.Details.RemoveAll(d => d.ProductId == 0);
            }

            if (model.Details == null || model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 hàng hóa hợp lệ.");
            }

            // KIỂM TRA DỮ LIỆU ĐẦU VÀO
            for (int i = 0; i < model.Details?.Count; i++)
            {
                var d = model.Details[i];
                
                if (d.DebitAccountId == null || d.DebitAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Nợ.");
                    
                if (d.CreditAccountId == null || d.CreditAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Có.");

                // Kiểm tra theo TaxRateSnapshot thay vì TaxId sẽ chính xác hơn
                if (d.TaxRateSnapshot > 0 && (d.TaxAccountId == null || d.TaxAccountId == 0))
                {
                    ModelState.AddModelError("", $"Dòng {i + 1}: Có phát sinh thuế, vui lòng chọn [TK Thuế].");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownData();
                if (string.IsNullOrEmpty(model.VoucherCode))
                {
                    model.VoucherCode = SalesVoucher.GenerateVoucherCode(await _context.SalesVouchers.CountAsync() + 1);
                }
                return View(model);
            }

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
            model.Status = VoucherStatus.Posted;

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

                // 🔥 BẢO VỆ SERVICE GHI SỔ KHỎI LỖI FOREIGN KEY 🔥
                // Nếu dòng này không có thuế, TaxAccountId đang là null sẽ khiến Service 
                // tự động chuyển thành ID = 0 và làm sập Database. 
                // Ta gán tạm nó bằng CreditAccountId để vượt qua lỗi khóa ngoại.
                if (detail.TaxAccountId == null || detail.TaxAccountId == 0)
                {
                    detail.TaxAccountId = detail.CreditAccountId;
                }

                model.TotalAmount += detail.Amount;
                model.TotalTaxAmount += detail.TaxAmount;
            }

            model.TotalPayment = model.TotalAmount + model.TotalTaxAmount;

            _context.SalesVouchers.Add(model);
            await _context.SaveChangesAsync();

            if (model.Status == VoucherStatus.Posted)
            {
                await _journalService.GenerateEntriesFromSalesVoucherAsync(model.Id);
            }

            TempData["SuccessMessage"] = $"Ghi nhận doanh thu thành công (Số CT: {model.VoucherCode})";

            if (submitAction == "save_and_new")
            {
                return RedirectToAction(nameof(Create));
            }

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

            // ĐÃ XÓA ĐIỀU KIỆN CHẶN TRẠNG THÁI (Giờ trạng thái Posted cũng xóa được)

            // ĐÃ THAY ĐỔI: Chuyển trạng thái sang Hủy (Canceled) thay vì xóa vật lý khỏi DB
            voucher.Status = VoucherStatus.Draft;; 
            // Lưu ý: Nếu có hàm xóa bút toán sổ cái tương ứng trong JournalEntryService, 
            // bạn nên gọi nó ở đây (vd: await _journalService.DeleteEntriesAsync(voucher.Id);)

            // Bỏ 2 dòng Remove cũ:
            // _context.SalesVoucherDetails.RemoveRange(voucher.Details);
            // _context.SalesVouchers.Remove(voucher);

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
            if (model.Details == null || model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 hàng hóa hợp lệ.");
            }

            // 👇 BỔ SUNG ĐOẠN CODE KIỂM TRA NÀY VÀO ĐÂY 👇
            for (int i = 0; i < model.Details.Count; i++)
            {
                var d = model.Details[i];
                
                // Kiểm tra TK Nợ / TK Có
                if (d.DebitAccountId == null || d.DebitAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Nợ.");
                    
                if (d.CreditAccountId == null || d.CreditAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Có.");

                // Kiểm tra nếu có thuế nhưng quên chọn TK Thuế
                if (d.TaxId != null && d.TaxId > 0 && (d.TaxAccountId == null || d.TaxAccountId == 0))
                {
                    ModelState.AddModelError("", $"Dòng {i + 1}: Có phát sinh thuế, vui lòng chọn [TK Thuế].");
                }
            }
            // 👆 KẾT THÚC ĐOẠN BỔ SUNG 👆

            if (!ModelState.IsValid)
            {
                await LoadDropdownData();
                return View(model);
            }

            var existing = await _context.SalesVouchers
                .Include(v => v.Details)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (existing == null) return NotFound();

            // Xóa detail cũ
            _context.SalesVoucherDetails.RemoveRange(existing.Details);

            // Update header
            existing.AccountingDate = model.AccountingDate;
            existing.CustomerId = model.CustomerId;
            existing.InvoiceNumber = model.InvoiceNumber;
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
                TaxRateSnapshot = d.TaxRateSnapshot,
                
                // 👇 BỔ SUNG CÁC TRƯỜNG TÀI KHOẢN BỊ THIẾU Ở ĐÂY 👇
                DebitAccountId = d.DebitAccountId,
                CreditAccountId = d.CreditAccountId,
                TaxId = d.TaxId,
                TaxAccountId = d.TaxAccountId
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



            // THÊM ĐOẠN NÀY: Nếu chứng từ đang là Posted mà bị sửa, 
            // ta gọi lại hàm GenerateEntries... để cập nhật lại (xóa bút toán cũ sinh bút toán mới) vào Sổ cái.
            if (existing.Status == VoucherStatus.Posted)
            {
                await _journalService.GenerateEntriesFromSalesVoucherAsync(existing.Id);
            }

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

            // Đổi trạng thái
            voucher.Status = VoucherStatus.Posted;
            await _context.SaveChangesAsync();

            // THÊM DÒNG NÀY: Sinh bút toán sổ cái sau khi ghi sổ thành công
            await _journalService.GenerateEntriesFromSalesVoucherAsync(voucher.Id);

            return Json(new { success = true });
        }
    }
}
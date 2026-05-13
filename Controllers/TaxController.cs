using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using Microsoft.AspNetCore.Authorization;

namespace RevenueAccountingMVC.Controllers
{
     
    public class TaxController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TaxController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================
        // INDEX
        // =======================
        [Authorize(Roles = "Accountant, Leader")] // CHỈ KẾ TOÁN VÀ LÃNH ĐẠO MỚI ĐƯỢC XEM DANH SÁCH THUẾ
        public async Task<IActionResult> Index(string searchString, int pageNumber = 1)
        {
            var query = _context.Taxes
                .Include(t => t.TaxAccount)
                .AsQueryable();

            // 1. Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(t =>
                    (t.TaxCode ?? "").Contains(searchString) ||
                    (t.TaxName ?? "").Contains(searchString));
            }

            // 2. Tính toán phân trang
            int pageSize = 10;
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var paginatedData = await query
                .OrderByDescending(t => t.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 3. Lưu lại trạng thái filter và phân trang
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalItems"] = totalItems;

            return View(paginatedData);
        }

        // =======================
        // CREATE
        // =======================
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Create()
        {
            await LoadAccounts();

            int next = await _context.Taxes.CountAsync() + 1;
            ViewBag.NextTaxCode = Tax.GenerateTaxCode(next);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Create(Tax tax, string submitAction)
        {
            if (ModelState.IsValid)
            {
                int next = await _context.Taxes.CountAsync() + 1;
                tax.TaxCode = Tax.GenerateTaxCode(next);

                tax.CreatedAt = DateTime.Now;

                _context.Add(tax);
                await _context.SaveChangesAsync();

                if (submitAction == "SaveAndNew")
                    return RedirectToAction(nameof(Create));

                return RedirectToAction(nameof(Index));
            }

            await LoadAccounts(tax.TaxAccountId);

            int retry = await _context.Taxes.CountAsync() + 1;
            ViewBag.NextTaxCode = Tax.GenerateTaxCode(retry);

            return View(tax);
        }

        // =======================
        // EDIT
        // =======================
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Edit(int id)
        {
            var tax = await _context.Taxes.FindAsync(id);
            if (tax == null) return NotFound();

            await LoadAccounts(tax.TaxAccountId);

            return View(tax);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Edit(int id, Tax tax)
        {
            if (id != tax.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var dbTax = await _context.Taxes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == id);

                    if (dbTax == null) return NotFound();

                    // ❗ KHÔNG cho sửa code
                    tax.TaxCode = dbTax.TaxCode;

                    tax.CreatedAt = dbTax.CreatedAt;
                    tax.UpdatedAt = DateTime.Now;

                    _context.Update(tax);
                    await _context.SaveChangesAsync();

                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    ModelState.AddModelError("", "Lỗi khi lưu dữ liệu");
                }
            }

            await LoadAccounts(tax.TaxAccountId);
            return View(tax);
        }

        // =======================
        // DETAILS
        // =======================
        [Authorize(Roles = "Accountant, Leader")]
        public async Task<IActionResult> Details(int id)
        {
            var tax = await _context.Taxes
                .Include(t => t.TaxAccount)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tax == null) return NotFound();

            return View(tax);
        }

        // =======================
        // TOGGLE
        // =======================
        [HttpPost]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var tax = await _context.Taxes.FindAsync(id);
            if (tax == null) return Json(new { success = false });

            tax.Status = tax.Status == TaxStatus.Active
                ? TaxStatus.Inactive
                : TaxStatus.Active;

            tax.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, status = (int)tax.Status });
        }

        // =======================
        // HELPER
        // =======================
        [Authorize(Roles = "Accountant, Leader")]
        private async Task LoadAccounts(int? selectedId = null)
        {
            var accounts = await _context.Accounts
                .Where(a => a.IsDetail == true)
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();

            ViewBag.Accounts = new SelectList(accounts, "Id", "AccountNumber", selectedId);
        }
    }
}
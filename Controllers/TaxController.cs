using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;

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
        public async Task<IActionResult> Index(string searchString)
        {
            var taxes = _context.Taxes
                .Include(t => t.TaxAccount)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                taxes = taxes.Where(t =>
                    (t.TaxCode ?? "").Contains(searchString) ||
                    (t.TaxName ?? "").Contains(searchString));
            }

            ViewData["CurrentSearch"] = searchString;

            return View(await taxes
                .OrderByDescending(t => t.Id)
                .ToListAsync());
        }

        // =======================
        // CREATE
        // =======================
        public async Task<IActionResult> Create()
        {
            await LoadAccounts();

            int next = await _context.Taxes.CountAsync() + 1;
            ViewBag.NextTaxCode = Tax.GenerateTaxCode(next);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public async Task<IActionResult> Edit(int id)
        {
            var tax = await _context.Taxes.FindAsync(id);
            if (tax == null) return NotFound();

            await LoadAccounts(tax.TaxAccountId);

            return View(tax);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
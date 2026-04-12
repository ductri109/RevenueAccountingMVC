using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;

namespace RevenueAccountingMVC.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================
        // INDEX
        // =======================
        public async Task<IActionResult> Index(string searchString)
        {
            // Tải toàn bộ danh sách để có thể tính toán cấp độ (Level)
            var allAccounts = await _context.Accounts
                .Include(a => a.ParentAccount)
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();

            var query = allAccounts.AsEnumerable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(a =>
                    a.AccountNumber.Contains(searchString) ||
                    a.AccountName.Contains(searchString));
            }

            ViewData["CurrentSearch"] = searchString;
            return View(query);
        }

        // =======================
        // CREATE (GET)
        // =======================
        public IActionResult Create()
        {
            LoadParentAccounts();
            return View();
        }

        // =======================
        // CREATE (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account account, string submitAction)
        {
            if (ModelState.IsValid)
            {
                // 🔥 CHECK UNIQUE AccountNumber
                bool exists = await _context.Accounts
                    .AnyAsync(a => a.AccountNumber == account.AccountNumber);

                if (exists)
                {
                    ModelState.AddModelError("AccountNumber", "Số tài khoản đã tồn tại");
                    LoadParentAccounts(account.ParentAccountId);
                    return View(account);
                }

                // 🔥 RULE: nếu có con → không phải detail
                if (account.ParentAccountId == null)
                {
                    var parent = await _context.Accounts.FindAsync(account.ParentAccountId);
                    if (parent != null && parent.IsDetail == true)
                    {
                        parent.IsDetail = false; // Khi có con, cha tự động mất quyền hạch toán
                        _context.Update(parent);
                    }
                }

                account.CreatedAt = DateTime.Now;

                _context.Add(account);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tạo tài khoản thành công";

                if (submitAction == "SaveAndNew")
                    return RedirectToAction(nameof(Create));

                return RedirectToAction(nameof(Index));
            }

            LoadParentAccounts(account.ParentAccountId);
            return View(account);
        }

        // =======================
        // EDIT (GET)
        // =======================
        public async Task<IActionResult> Edit(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound();

            LoadParentAccounts(account.ParentAccountId, id);
            return View(account);
        }

        // =======================
        // EDIT (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account account)
        {
            if (id != account.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var dbAccount = await _context.Accounts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Id == id);

                    if (dbAccount == null) return NotFound();

                    // 🔥 KHÔNG cho sửa AccountNumber
                    account.AccountNumber = dbAccount.AccountNumber;
                    account.CreatedAt = dbAccount.CreatedAt;
                    account.UpdatedAt = DateTime.Now;

                    // 🔥 CHECK: không cho chọn chính nó làm cha
                    if (account.ParentAccountId == id)
                    {
                        ModelState.AddModelError("ParentAccountId", "Không thể chọn chính nó làm tài khoản mẹ");
                        LoadParentAccounts(account.ParentAccountId, id);
                        return View(account);
                    }

                    _context.Update(account);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cập nhật thành công";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    ModelState.AddModelError("", "Lỗi khi lưu dữ liệu");
                }
            }

            LoadParentAccounts(account.ParentAccountId, id);
            return View(account);
        }

        // =======================
        // DETAILS
        // =======================
        public async Task<IActionResult> Details(int id)
        {
            var account = await _context.Accounts
                .Include(a => a.ParentAccount)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account == null) return NotFound();
            return View(account);
        }

        // =======================
        // TOGGLE STATUS
        // =======================
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return Json(new { success = false });

            account.Status = account.Status == AccountStatus.Active
                ? AccountStatus.Inactive
                : AccountStatus.Active;

            account.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Json(new { success = true, status = (int)account.Status });
        }

        // =======================
        // HELPER
        // =======================
        private void LoadParentAccounts(int? selectedId = null, int? excludeId = null)
        {
            var query = _context.Accounts.AsQueryable();

            if (excludeId.HasValue)
            {
                query = query.Where(a => a.Id != excludeId.Value);
            }

            ViewBag.ParentAccounts = new SelectList(
                query.OrderBy(a => a.AccountNumber),
                "Id",
                "AccountNumber",
                selectedId
            );
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;

namespace RevenueAccountingMVC.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CustomerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================
        // INDEX
        // =======================
        public async Task<IActionResult> Index(string searchString)
        {
            var customers = _context.Customers
                .Include(c => c.ReceivableAccount) // 🔥 FIX: load account
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                customers = customers.Where(c =>
                    (c.CustomerCode ?? "").Contains(searchString) ||
                    (c.CustomerName ?? "").Contains(searchString) ||
                    (c.PhoneNumber ?? "").Contains(searchString) ||
                    (c.Email ?? "").Contains(searchString));
            }

            ViewData["CurrentSearch"] = searchString;

            return View(await customers
                .OrderByDescending(c => c.Id)
                .ToListAsync());
        }

        // =======================
        // CREATE (GET)
        // =======================
        public async Task<IActionResult> Create()
        {
            await LoadAccounts();

            // 🔥 FIX: generate code kiểu mới
            int next = await _context.Customers.CountAsync() + 1;
            ViewBag.NextCustomerCode = Customer.GenerateCustomerCode(next);

            return View();
        }

        // =======================
        // CREATE (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer, string submitAction)
        {
            if (ModelState.IsValid)
            {
                // 🔥 FIX: generate code an toàn hơn
                int next = await _context.Customers.CountAsync() + 1;
                customer.CustomerCode = Customer.GenerateCustomerCode(next);

                customer.CreatedAt = DateTime.Now;

                _context.Add(customer);
                await _context.SaveChangesAsync();

                if (submitAction == "SaveAndNew")
                {
                    TempData["SuccessMessage"] = $"Đã lưu {customer.CustomerCode}";
                    return RedirectToAction(nameof(Create));
                }

                TempData["SuccessMessage"] = "Thêm khách hàng thành công";
                return RedirectToAction(nameof(Index));
            }

            await LoadAccounts(customer.ReceivableAccountId);

            int retryNext = await _context.Customers.CountAsync() + 1;
            ViewBag.NextCustomerCode = Customer.GenerateCustomerCode(retryNext);

            return View(customer);
        }

        // =======================
        // EDIT (GET)
        // =======================
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            await LoadAccounts(customer.ReceivableAccountId);

            return View(customer);
        }

        // =======================
        // EDIT (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var dbCustomer = await _context.Customers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Id == id);

                    if (dbCustomer == null) return NotFound();

                    // 🔥 KHÔNG cho sửa code
                    customer.CustomerCode = dbCustomer.CustomerCode;

                    customer.CreatedAt = dbCustomer.CreatedAt;
                    customer.UpdatedAt = DateTime.Now;

                    _context.Update(customer);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cập nhật thành công";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    ModelState.AddModelError("", "Lỗi khi lưu dữ liệu");
                }
            }

            await LoadAccounts(customer.ReceivableAccountId);
            return View(customer);
        }

        // =======================
        // DETAILS
        // =======================
        public async Task<IActionResult> Details(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.ReceivableAccount) // 🔥 FIX
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null) return NotFound();

            return View(customer);
        }

        // =======================
        // TOGGLE STATUS
        // =======================
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return Json(new { success = false });

            customer.Status = customer.Status == CustomerStatus.Active
                ? CustomerStatus.Inactive
                : CustomerStatus.Active;

            customer.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, status = (int)customer.Status });
        }

        // =======================
        // HELPER: LOAD ACCOUNT
        // =======================
        private async Task LoadAccounts(int? selectedId = null)
        {
            var accounts = await _context.Accounts
                .Where(a => a.IsDetail == true) // 🔥 CHỈ cho chọn TK hạch toán
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();

            ViewBag.Accounts = new SelectList(accounts, "Id", "AccountNumber", selectedId);
        }
    }
}
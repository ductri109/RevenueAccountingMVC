using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using Microsoft.AspNetCore.Authorization;

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
        [Authorize(Roles = "Accountant, Leader")]
        public async Task<IActionResult> Index(string searchString, int? customerType, int pageNumber = 1)
        {
            var query = _context.Customers
                .Include(c => c.ReceivableAccount)
                .AsQueryable();

            // 1. Lọc theo từ khóa (Mã, Tên, SĐT, Email)
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(c =>
                    (c.CustomerCode ?? "").Contains(searchString) ||
                    (c.CustomerName ?? "").Contains(searchString) ||
                    (c.PhoneNumber ?? "").Contains(searchString) ||
                    (c.Email ?? "").Contains(searchString));
            }

            // 2. Lọc theo loại hình khách hàng
            if (customerType.HasValue)
            {
                query = query.Where(c => (int)c.CustomerType == customerType.Value);
            }

            // 3. Tính toán phân trang
            int pageSize = 10;
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var paginatedData = await query
                .OrderByDescending(c => c.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 4. Lưu lại filter state
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentCustomerType"] = customerType;
            
            // 5. Lưu lại phân trang
            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalItems"] = totalItems;

            return View(paginatedData);
        }

        // =======================
        // CREATE (GET)
        // =======================
        [Authorize(Roles = "Accountant")]
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
        [Authorize(Roles = "Accountant")]
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
        [Authorize(Roles = "Accountant")]
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
        [Authorize(Roles = "Accountant")]
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
        [Authorize(Roles = "Accountant, Leader")] // CHỈ KẾ TOÁN VÀ LÃNH ĐẠO MỚI ĐƯỢC XEM CHI TIẾT KHÁCH HÀNG
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
        [Authorize(Roles = "Accountant")]
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
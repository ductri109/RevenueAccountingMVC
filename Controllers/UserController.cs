using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.ViewModels;

namespace RevenueAccountingMVC.Controllers
{
    [Authorize(Roles = "Admin")] // KHOÁ CHẶT: Chỉ Admin mới được vào
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Danh sách người dùng
        public async Task<IActionResult> Index()
        {
            // Lấy danh sách, trừ tài khoản đang đăng nhập (để tránh Admin tự khóa chính mình)
            var currentUserId = int.Parse(User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            var users = await _context.Users
                                      .Where(u => u.Id != currentUserId)
                                      .OrderByDescending(u => u.CreatedAt)
                                      .ToListAsync();
            return View(users);
        }

        // 2. Mở form thêm mới
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 3. Xử lý thêm mới
        [HttpPost]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Check trùng Email
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email này đã tồn tại trong hệ thống.");
                return View(model);
            }

            var user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                Role = model.Role,
                IsActive = true,
                CreatedAt = DateTime.Now,
                // Băm mật khẩu (Nhớ cài package BCrypt.Net-Next)
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo tài khoản thành công!";
            return RedirectToAction(nameof(Index));
        }

        // 4. Khóa / Mở khóa tài khoản
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive; // Đảo trạng thái (True -> False, False -> True)
            await _context.SaveChangesAsync();

            TempData["Success"] = user.IsActive ? $"Đã mở khóa tài khoản {user.Email}" : $"Đã khóa tài khoản {user.Email}";
            return RedirectToAction(nameof(Index));
        }
    }
}
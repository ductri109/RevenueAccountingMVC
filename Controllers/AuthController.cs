using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using System.Security.Claims;

namespace RevenueAccountingMVC.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- ĐĂNG NHẬP ADMIN / KẾ TOÁN ---
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Dùng FirstOrDefaultAsync để tối ưu hiệu năng
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            
            // Check null, check IsActive, và check Mật khẩu bằng BCrypt
            if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác, hoặc đã bị khóa.");
                return View(model);
            }

            if (user.Role == UserRole.Leader)
            {
                ModelState.AddModelError(string.Empty, "Lãnh đạo vui lòng sử dụng Cổng đăng nhập dành cho Lãnh đạo.");
                return View(model);
            }

            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }

        // --- ĐĂNG NHẬP LÃNH ĐẠO ---
        [HttpGet]
        public IActionResult LoginLeader() => View();

        [HttpPost]
        public async Task<IActionResult> LoginLeader(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            
            if (user == null || user.Role != UserRole.Leader || !user.IsActive || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không hợp lệ hoặc bạn không có quyền Lãnh đạo.");
                return View(model);
            }

            await SignInUser(user);
            return RedirectToAction("Index", "Home");
        }

        // --- ĐĂNG KÝ (CHỈ DÀNH CHO KẾ TOÁN) ---
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Cũng dùng Async nốt
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError("Email", "Email này đã được sử dụng.");
                return View(model);
            }

            var newUser = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                // Mã hóa mật khẩu siêu xịn
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password), 
                Role = UserRole.Accountant, 
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync(); // Lưu bằng Async

            TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        // --- ĐĂNG XUẤT ---
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // Hàm hỗ trợ ghi Cookie Đăng nhập
        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
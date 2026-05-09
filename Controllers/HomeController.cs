using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RevenueAccountingMVC.Models;


namespace RevenueAccountingMVC.Controllers;
[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        // Kiểm tra xem đã đăng nhập chưa và có phải là Admin không
        if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Admin"))
        {
            // Nếu là Admin, tự động điều hướng sang Controller "User", Action "Index"
            return RedirectToAction("Index", "User");
        }

        // Nếu là Kế toán hoặc Lãnh đạo thì vẫn hiển thị trang Dashboard bình thường
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous] // Riêng trang lỗi thì cho phép ai cũng xem được (không cần đăng nhập)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

}

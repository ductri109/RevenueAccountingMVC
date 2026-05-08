using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting; // Thêm thư viện này
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RevenueAccountingMVC.Controllers
{
    public class DataImportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DataImportService _importService;
        private readonly IWebHostEnvironment _env;

        // Tối ưu: Tiêm DataImportService và IWebHostEnvironment qua DI
        public DataImportController(ApplicationDbContext context, DataImportService importService, IWebHostEnvironment env)
        {
            _context = context;
            _importService = importService;
            _env = env;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Import()
        {
            try
            {
                // Tối ưu: Dùng _env.ContentRootPath để lấy chuẩn đường dẫn khi Publish
                var folderPath = Path.Combine(_env.ContentRootPath, "Data", "Import");

                if (!Directory.Exists(folderPath))
                {
                    TempData["Error"] = "Thư mục Import không tồn tại! Vui lòng tạo thư mục Data/Import và copy file CSV vào.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _importService.ImportAllAsync(folderPath);

                TempData["Success"] = result;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi hệ thống: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportFromPath(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                {
                    return Json(new { success = false, message = "Đường dẫn không hợp lệ hoặc không tồn tại!" });
                }

                var result = await _importService.ImportAllAsync(folderPath);

                return Json(new { success = true, message = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }
    }
}
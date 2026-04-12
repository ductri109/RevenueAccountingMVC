using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;

namespace RevenueAccountingMVC.Controllers
{
    [Authorize]
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================
        // INDEX
        // =======================
        public async Task<IActionResult> Index(string searchString, string productType)
        {
            // 1. Khởi tạo truy vấn
            var products = _context.Products
                .Include(p => p.RevenueAccount)
                .Include(p => p.InventoryAccount)
                .Include(p => p.Tax)
                .AsQueryable();

            // 2. Lọc theo tên/mã sản phẩm (nếu có)
            if (!string.IsNullOrEmpty(searchString))
            {
                products = products.Where(p =>
                    (p.ProductCode ?? "").Contains(searchString) ||
                    (p.ProductName ?? "").Contains(searchString));
            }

            // 3. LỌC THEO LOẠI SẢN PHẨM (Đây là phần bạn cần thêm)
            if (!string.IsNullOrEmpty(productType))
            {
                // Giả sử: 1 = Goods, 2 = Services (Dựa theo Enum của bạn)
                // Lưu ý: Ép kiểu sang Enum tương ứng
                if (Enum.TryParse(productType, out ProductType typeEnum))
                {
                    products = products.Where(p => p.ProductType == typeEnum);
                }
            }

            // 4. Lưu lại giá trị lọc để hiển thị trên View
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentProductType"] = productType;

            return View(await products.OrderByDescending(p => p.Id).ToListAsync());
        }

        // =======================
        // CREATE (GET)
        // =======================
        public async Task<IActionResult> Create()
        {
            await LoadData();

            int next = await _context.Products.CountAsync() + 1;
            ViewBag.NextProductCode = Product.GenerateProductCode(next);

            return View();
        }

        // =======================
        // CREATE (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, string submitAction)
        {
            if (ModelState.IsValid)
            {
                int next = await _context.Products.CountAsync() + 1;
                product.ProductCode = Product.GenerateProductCode(next);

                product.CreatedAt = DateTime.Now;

                _context.Add(product);
                await _context.SaveChangesAsync();

                if (submitAction == "SaveAndNew")
                {
                    TempData["SuccessMessage"] = $"Đã lưu {product.ProductCode}";
                    return RedirectToAction(nameof(Create));
                }

                return RedirectToAction(nameof(Index));
            }

            await LoadData(product.RevenueAccountId, product.InventoryAccountId, product.TaxId);

            int retryNext = await _context.Products.CountAsync() + 1;
            ViewBag.NextProductCode = Product.GenerateProductCode(retryNext);

            return View(product);
        }

        // =======================
        // EDIT
        // =======================
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            await LoadData(product.RevenueAccountId, product.InventoryAccountId, product.TaxId);

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    var dbProduct = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == id);

                    if (dbProduct == null) return NotFound();

                    // ❗ KHÔNG cho sửa code
                    product.ProductCode = dbProduct.ProductCode;

                    product.CreatedAt = dbProduct.CreatedAt;
                    product.UpdatedAt = DateTime.Now;

                    _context.Update(product);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cập nhật thành công";
                    return RedirectToAction(nameof(Index));
                }
                catch
                {
                    ModelState.AddModelError("", "Lỗi khi lưu dữ liệu");
                }
            }

            await LoadData(product.RevenueAccountId, product.InventoryAccountId, product.TaxId);
            return View(product);
        }

        // =======================
        // DETAILS
        // =======================
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.RevenueAccount)
                .Include(p => p.InventoryAccount)
                .Include(p => p.Tax)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            return View(product);
        }

        // =======================
        // TOGGLE STATUS
        // =======================
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return Json(new { success = false });

            product.Status = product.Status == ProductStatus.Active
                ? ProductStatus.Inactive
                : ProductStatus.Active;

            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return Json(new { success = true, status = (int)product.Status });
        }

        // =======================
        // HELPER
        // =======================
        private async Task LoadData(int? revenueId = null, int? inventoryId = null, int? taxId = null)
        {
            var accounts = await _context.Accounts
                .Where(a => a.IsDetail == true)
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();

            var taxes = await _context.Taxes
                .Where(t => t.Status == TaxStatus.Active)
                .ToListAsync();

            ViewBag.RevenueAccounts = new SelectList(accounts, "Id", "AccountNumber", revenueId);
            ViewBag.InventoryAccounts = new SelectList(accounts, "Id", "AccountNumber", inventoryId);
            ViewBag.Taxes = new SelectList(taxes, "Id", "TaxName", taxId);
        }
    }
}
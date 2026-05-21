using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.Services;
using Microsoft.AspNetCore.Authorization;

namespace RevenueAccountingMVC.Controllers
{
    public class SalesVoucherController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly JournalEntryService _journalService;

        public SalesVoucherController(ApplicationDbContext context, JournalEntryService journalService)
        {
            _context = context;
            _journalService = journalService;
        }

        // =======================
        // 1. INDEX - Danh sách
        // =======================
        [Authorize(Roles = "Accountant, Leader")]
        public async Task<IActionResult> Index(string searchString, int pageNumber = 1)
        {
            var query = _context.SalesVouchers
                .Include(v => v.Customer)
                // THÊM DÒNG NÀY: Chỉ hiển thị các chứng từ có Status là Posted
                .Where(v => v.Status == VoucherStatus.Posted)
                .AsQueryable();

            // 1. Lọc theo từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(v =>
                    v.VoucherCode.Contains(searchString) ||
                    (v.CustomerNameSnapshot != null && v.CustomerNameSnapshot.Contains(searchString)));
            }

            // 2. Tính tổng thanh toán của TẤT CẢ bản ghi (đã lọc) trước khi phân trang
            decimal totalAmount = await query.SumAsync(v => v.TotalPayment);

            // 3. Tính toán phân trang
            int pageSize = 10;
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (pageNumber < 1) pageNumber = 1;
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var paginatedData = await query
                .OrderByDescending(v => v.AccountingDate)
                .ThenByDescending(v => v.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 4. Lưu lại trạng thái filter, tổng tiền và phân trang
            ViewData["CurrentSearch"] = searchString;
            ViewData["CurrentPage"] = pageNumber;
            ViewData["TotalPages"] = totalPages;
            ViewData["TotalItems"] = totalItems;
            ViewData["TotalAmount"] = totalAmount;

            return View(paginatedData);
        }

        // =======================
        // 2. CREATE (GET)
        // =======================
        [Authorize(Roles = "Accountant")] // 1. Chỉ người dùng có vai trò "Accountant" (Kế toán) mới được truy cập vào đây.
        public async Task<IActionResult> Create() // 2. Khai báo phương thức bất đồng bộ (async), trả về một giao diện (View).
        {
            await LoadDropdownData(); // 3. Tải danh sách đổ xuống (như khách hàng, kho, tài khoản kế toán...) lên giao diện.

            int next = await _context.SalesVouchers.CountAsync() + 1; // 4. Đếm tổng số chứng từ hiện có trong database và cộng thêm 1 để tính số thứ tự tiếp theo.
            ViewBag.NextCode = SalesVoucher.GenerateVoucherCode(next); // 5. Gửi số mã chứng từ dự kiến qua ViewBag để hiển thị lên giao diện (ví dụ: HD0004).

            var model = new SalesVoucher(); // 6. Khởi tạo một đối tượng chứng từ bán hàng mới hoàn toàn.
            model.VoucherCode = SalesVoucher.GenerateVoucherCode(next); // 7. Gán mã chứng từ vừa tính được vào model.
            model.Details.Add(new SalesVoucherDetail()); // 8. Thêm sẵn một dòng chi tiết trống (mặc định mở ra có sẵn 1 dòng để nhập hàng hóa).
            model.AccountingDate = DateTime.Now.Date; // 9. Lấy ngày hiện tại (bỏ đi phần giờ phút giây) gán làm ngày hạch toán mặc định.

            return View(model); // 10. Truyền dữ liệu model này sang giao diện hiển thị cho người dùng nhập.
        }

        // =======================
        // 3. CREATE (POST)
        // =======================
        [HttpPost] // 11. Đánh dấu phương thức này chỉ nhận dữ liệu gửi lên (Submit form).
        [ValidateAntiForgeryToken] // 12. Bảo mật chống tấn công giả mạo yêu cầu (CSRF).
        [Authorize(Roles = "Accountant")] // 13. Tiếp tục phân quyền chỉ kế toán mới được gửi dữ liệu lên.
        public async Task<IActionResult> Create(SalesVoucher model, string submitAction) // 14. Tiếp nhận dữ liệu từ form (model) và tên của nút bấm (submitAction).
        {
            // LỌC DÒNG RỖNG
            if (model.Details != null)
            {
                model.Details.RemoveAll(d => d.ProductId == 0); // 15. Xóa bỏ tất cả các dòng chi tiết mà kế toán bấm thêm nhưng không chọn hàng hóa (ProductId = 0).
            }

            // KIỂM TRA SỐ LƯỢNG DÒNG
            if (model.Details == null || model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 hàng hóa hợp lệ."); // 16. Nếu không có dòng hàng hóa nào, báo lỗi lên màn hình.
            }

            // KIỂM TRA DỮ LIỆU ĐẦU VÀO CỦA TỪNG DÒNG (VALIDATION)
            for (int i = 0; i < model.Details?.Count; i++) // 17. Chạy vòng lặp kiểm tra qua từng dòng chi tiết hàng hóa.
            {
                var d = model.Details[i]; // 18. Lấy ra dòng chi tiết thứ i.
                
                if (d.DebitAccountId == null || d.DebitAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Nợ."); // 19. Bắt buộc chọn Tài khoản Nợ.
                    
                if (d.CreditAccountId == null || d.CreditAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Có."); // 20. Bắt buộc chọn Tài khoản Có.

                // Kiểm tra tài khoản thuế nếu có phần trăm thuế
                if (d.TaxRateSnapshot > 0 && (d.TaxAccountId == null || d.TaxAccountId == 0))
                {
                    ModelState.AddModelError("", $"Dòng {i + 1}: Có phát sinh thuế, vui lòng chọn [TK Thuế]."); // 21. Nếu dòng này có thuế (VD: 10%) thì bắt buộc phải nhập Tài khoản thuế.
                }
            }

            // XỬ LÝ KHI CÓ LỖI NHẬP LIỆU
            if (!ModelState.IsValid) // 22. Nếu có bất kỳ lỗi nào ở trên hoặc lỗi từ hệ thống form:
            {
                await LoadDropdownData(); // 23. Nạp lại các danh sách đổ xuống để giao diện không bị lỗi hiển thị.
                if (string.IsNullOrEmpty(model.VoucherCode))
                {
                    model.VoucherCode = SalesVoucher.GenerateVoucherCode(await _context.SalesVouchers.CountAsync() + 1); // 24. Nếu mã chứng từ bị mất, tạo lại mã mới.
                }
                return View(model); // 25. Trả về giao diện kèm theo các thông báo lỗi để người dùng sửa lại.
            }

            // CHUẨN BỊ TÍNH TOÁN VÀ LƯU TRỮ
            model.TotalAmount = 0; // 26. Đặt tổng tiền hàng trước thuế về 0 để tính toán lại từ đầu.
            model.TotalTaxAmount = 0; // 27. Đặt tổng tiền thuế về 0.

            // LƯU THÔNG TIN KHÁCH HÀNG TẠI THỜI ĐIỂM BÁN (SNAPSHOT)
            var customer = await _context.Customers.FindAsync(model.CustomerId); // 28. Tìm kiếm thông tin khách hàng trong DB bằng CustomerId.
            if (customer != null)
            {
                model.CustomerNameSnapshot = customer.CustomerName; // 29. Lưu lại tên khách hàng lúc bấy giờ (tránh việc sau này khách đổi tên làm sai lệch hóa đơn cũ).
                model.CustomerAddressSnapshot = customer.Address; // 30. Lưu lại địa chỉ khách hàng tại thời điểm mua.
            }

            model.DueDate = model.AccountingDate.AddDays(model.DebtDays); // 31. Tính ngày hạn thanh toán = Ngày hạch toán + Số ngày cho nợ.
            model.CreatedAt = DateTime.Now; // 32. Ghi nhận thời gian tạo chứng từ là thời gian hiện tại của máy chủ.
            model.Status = VoucherStatus.Posted; // 33. Chuyển trạng thái chứng từ thành "Posted" (Đã ghi sổ).

            // TÍNH TOÁN CHI TIẾT TỪNG DÒNG HÀNG
            foreach (var detail in model.Details) // 34. Duyệt qua từng dòng hàng hóa đã nhập hợp lệ.
            {
                var product = await _context.Products.FindAsync(detail.ProductId); // 35. Tìm thông tin sản phẩm trong DB.
                if (product != null)
                {
                    detail.ProductNameSnapshot = product.ProductName; // 36. Lưu tên sản phẩm tại thời điểm bán.
                    detail.UnitSnapshot = product.Unit; // 37. Lưu đơn vị tính tại thời điểm bán.
                }

                // Công thức tính thành tiền: Số lượng * Đơn giá * (1 - % Chiết khấu)
                detail.Amount = detail.Quantity * detail.UnitPrice * (1 - (detail.DiscountRate / 100m)); // 38. Tính thành tiền sau khi trừ chiết khấu.
                detail.TaxAmount = detail.Amount * (detail.TaxRateSnapshot / 100m); // 39. Tính tiền thuế dựa trên số tiền sau chiết khấu.

                // MẸO TRÁNH LỖI CƠ SỞ DỮ LIỆU
                if (detail.TaxAccountId == null || detail.TaxAccountId == 0)
                {
                    detail.TaxAccountId = detail.CreditAccountId; // 40. Nếu dòng này không chịu thuế, gán ID tài khoản thuế bằng tài khoản có để không bị lỗi ràng buộc khóa ngoại (Foreign Key) trong database.
                }

                model.TotalAmount += detail.Amount; // 41. Cộng dồn tiền hàng vào tổng tiền hàng của chứng từ.
                model.TotalTaxAmount += detail.TaxAmount; // 42. Cộng dồn tiền thuế vào tổng tiền thuế của chứng từ.
            }

            model.TotalPayment = model.TotalAmount + model.TotalTaxAmount; // 43. Tổng thanh toán = Tổng tiền hàng + Tổng tiền thuế.

            // LƯU CHỨNG TỪ VÀO DATABASE
            _context.SalesVouchers.Add(model); // 44. Thêm chứng từ bán hàng này vào danh sách theo dõi của Entity Framework.
            await _context.SaveChangesAsync(); // 45. Thực thi lưu chính thức vào Cơ sở dữ liệu và sinh ra `model.Id`.

            // TỰ ĐỘNG SINH BÚT TOÁN NHẬT KÝ CHUNG
            if (model.Status == VoucherStatus.Posted)
            {
                await _journalService.GenerateEntriesFromSalesVoucherAsync(model.Id); // 46. Gọi dịch vụ kế toán để tự động định khoản Nợ/Có vào sổ nhật ký chung dựa trên ID hóa đơn vừa lưu.
            }

            TempData["SuccessMessage"] = $"Ghi nhận doanh thu thành công (Số CT: {model.VoucherCode})"; // 47. Tạo thông báo thành công hiển thị cho người dùng ở trang tiếp theo.

            // ĐIỀU HƯỚNG SAU KHI LƯU
            if (submitAction == "save_and_new")
            {
                return RedirectToAction(nameof(Create)); // 48. Nếu bấm "Lưu và Thêm mới", quay lại trang tạo mới trống.
            }

            return RedirectToAction(nameof(Index)); // 49. Nếu bấm "Lưu" thông thường, quay về trang danh sách chứng từ.
        }

        // =======================
        // 4. DELETE
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Delete(int id)
        {
            var voucher = await _context.SalesVouchers
                .Include(v => v.Details)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null)
                return Json(new { success = false, message = "Không tìm thấy chứng từ." });

            // ĐÃ XÓA ĐIỀU KIỆN CHẶN TRẠNG THÁI (Giờ trạng thái Posted cũng xóa được)

            // ĐÃ THAY ĐỔI: Chuyển trạng thái sang Hủy (Canceled) thay vì xóa vật lý khỏi DB
            voucher.Status = VoucherStatus.Draft;; 
            // Lưu ý: Nếu có hàm xóa bút toán sổ cái tương ứng trong JournalEntryService, 
            // bạn nên gọi nó ở đây (vd: await _journalService.DeleteEntriesAsync(voucher.Id);)

            // Bỏ 2 dòng Remove cũ:
            // _context.SalesVoucherDetails.RemoveRange(voucher.Details);
            // _context.SalesVouchers.Remove(voucher);

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // =======================
        // 5. DETAILS
        // =======================
        [Authorize(Roles = "Accountant, Leader")]
        public async Task<IActionResult> Details(int id)
        {
            var voucher = await _context.SalesVouchers
                .Include(v => v.Customer)
                .Include(v => v.Details)
                .ThenInclude(d => d.Product)
                .Include(s => s.Details)
                    .ThenInclude(d => d.DebitAccount)    // Dòng này load TK Nợ
                .Include(s => s.Details)
                    .ThenInclude(d => d.CreditAccount)   // Dòng này load TK Có
                .Include(s => s.Details)
                    .ThenInclude(d => d.TaxAccount)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null) return NotFound();

            return View(voucher);
        }

        // =======================
        // 6. EDIT (GET)
        // =======================
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Edit(int id)
        {
            var voucher = await _context.SalesVouchers
                .Include(v => v.Details)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null) return NotFound();

            await LoadDropdownData();
            return View(voucher);
        }

        // =======================
        // 7. EDIT (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Edit(int id, SalesVoucher model)
        {
            if (id != model.Id) return NotFound();

            if (model.Details != null)
            {
                model.Details.RemoveAll(d => d.ProductId == 0);
            }

            if (model.Details == null || model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 hàng hóa hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownData();
                return View(model);
            }
            if (model.Details == null || model.Details.Count == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn ít nhất 1 hàng hóa hợp lệ.");
            }

            // 👇 BỔ SUNG ĐOẠN CODE KIỂM TRA NÀY VÀO ĐÂY 👇
            for (int i = 0; i < model.Details.Count; i++)
            {
                var d = model.Details[i];
                
                // Kiểm tra TK Nợ / TK Có
                if (d.DebitAccountId == null || d.DebitAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Nợ.");
                    
                if (d.CreditAccountId == null || d.CreditAccountId == 0)
                    ModelState.AddModelError("", $"Dòng {i + 1}: Vui lòng chọn TK Có.");

                // Kiểm tra nếu có thuế nhưng quên chọn TK Thuế
                if (d.TaxId != null && d.TaxId > 0 && (d.TaxAccountId == null || d.TaxAccountId == 0))
                {
                    ModelState.AddModelError("", $"Dòng {i + 1}: Có phát sinh thuế, vui lòng chọn [TK Thuế].");
                }
            }
            // 👆 KẾT THÚC ĐOẠN BỔ SUNG 👆

            if (!ModelState.IsValid)
            {
                await LoadDropdownData();
                return View(model);
            }

            var existing = await _context.SalesVouchers
                .Include(v => v.Details)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (existing == null) return NotFound();

            // Xóa detail cũ
            _context.SalesVoucherDetails.RemoveRange(existing.Details);

            // Update header
            existing.AccountingDate = model.AccountingDate;
            existing.CustomerId = model.CustomerId;
            existing.InvoiceNumber = model.InvoiceNumber;
            var customer = await _context.Customers.FindAsync(model.CustomerId);
            if (customer != null)
            {
                existing.CustomerNameSnapshot = customer.CustomerName;
                existing.CustomerAddressSnapshot = customer.Address;
            }

            existing.Description = model.Description;
            existing.DebtDays = model.DebtDays;
            existing.DueDate = model.AccountingDate.AddDays(model.DebtDays);

            existing.TotalAmount = 0;
            existing.TotalTaxAmount = 0;

            // Clone detail chuẩn EF
            existing.Details = model.Details.Select(d => new SalesVoucherDetail
            {
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                DiscountRate = d.DiscountRate,
                TaxRateSnapshot = d.TaxRateSnapshot,
                
                // 👇 BỔ SUNG CÁC TRƯỜNG TÀI KHOẢN BỊ THIẾU Ở ĐÂY 👇
                DebitAccountId = d.DebitAccountId,
                CreditAccountId = d.CreditAccountId,
                TaxId = d.TaxId,
                TaxAccountId = d.TaxAccountId
            }).ToList();

            foreach (var detail in existing.Details)
            {
                var product = await _context.Products.FindAsync(detail.ProductId);
                if (product != null)
                {
                    detail.ProductNameSnapshot = product.ProductName;
                    detail.UnitSnapshot = product.Unit;
                }

                detail.Amount = detail.Quantity * detail.UnitPrice * (1 - detail.DiscountRate / 100m);
                detail.TaxAmount = detail.Amount * (detail.TaxRateSnapshot / 100m);

                existing.TotalAmount += detail.Amount;
                existing.TotalTaxAmount += detail.TaxAmount;
            }

            existing.TotalPayment = existing.TotalAmount + existing.TotalTaxAmount;

            await _context.SaveChangesAsync();



            // THÊM ĐOẠN NÀY: Nếu chứng từ đang là Posted mà bị sửa, 
            // ta gọi lại hàm GenerateEntries... để cập nhật lại (xóa bút toán cũ sinh bút toán mới) vào Sổ cái.
            if (existing.Status == VoucherStatus.Posted)
            {
                await _journalService.GenerateEntriesFromSalesVoucherAsync(existing.Id);
            }

            TempData["SuccessMessage"] = "Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }

        // =======================
        // HELPER DROPDOWNS
        // =======================
        [Authorize(Roles = "Accountant, Leader")]
        private async Task LoadDropdownData()
        {
            var customers = await _context.Customers
                .Where(c => c.Status == CustomerStatus.Active)
                .OrderBy(c => c.CustomerCode)
                .ToListAsync();

            ViewBag.Customers = new SelectList(customers, "Id", "CustomerCode");

            ViewBag.CustomersData = customers.ToDictionary(
                c => c.Id,
                c => new { c.CustomerName, c.Address, c.MaxDebtDays, c.ReceivableAccountId }
            );

            var products = await _context.Products
                .Include(p => p.Tax)
                .Where(p => p.Status == ProductStatus.Active)
                .ToListAsync();

            ViewBag.Products = new SelectList(products, "Id", "ProductCode");

            ViewBag.ProductsData = products.ToDictionary(
                p => p.Id,
                p => new
                {
                    p.ProductName,
                    p.Unit,
                    p.DefaultUnitPrice,
                    p.RevenueAccountId,
                    p.TaxId,
                    TaxAccountId = p.Tax?.TaxAccountId
                }
            );

            // Bỏ Where(a => a.IsDetail) để lấy tất cả tài khoản, thêm OrderBy để cha con đứng gần nhau
            ViewBag.Accounts = new SelectList(
                await _context.Accounts.OrderBy(a => a.AccountNumber).ToListAsync(),
                "Id",
                "AccountNumber"
            );

            ViewBag.Taxes = new SelectList(
                await _context.Taxes.Where(t => t.Status == TaxStatus.Active).ToListAsync(),
                "Id",
                "TaxName"
            );
        }

        // =======================
        // API AUTO FILL
        // =======================
        [HttpGet]
        public async Task<IActionResult> GetCustomerDefaults(int id)
        {
            var c = await _context.Customers.FindAsync(id);
            if (c == null) return NotFound();

            return Json(new
            {
                customerName = c.CustomerName,
                address = c.Address,
                maxDebtDays = c.MaxDebtDays ?? 0,
                receivableAccountId = c.ReceivableAccountId
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDefaults(int id)
        {
            var p = await _context.Products
                .Include(x => x.Tax)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            return Json(new
            {
                productName = p.ProductName,
                unit = p.Unit,
                defaultPrice = p.DefaultUnitPrice ?? 0,
                revenueAccountId = p.RevenueAccountId,
                taxId = p.TaxId,
                taxRate = p.DefaultTaxRate ?? (p.Tax != null ? p.Tax.TaxRate : 0),
                taxAccountId = p.Tax != null ? (int?)p.Tax.TaxAccountId : null
            });
        }

        [HttpPost]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> PostVoucher(int id)
        {
            var voucher = await _context.SalesVouchers.FindAsync(id);
            if (voucher == null) return NotFound();

            if (voucher.Status == VoucherStatus.Posted)
            {
                return Json(new { success = false, message = "Chứng từ đã ghi sổ rồi!" });
            }

            // Đổi trạng thái
            voucher.Status = VoucherStatus.Posted;
            await _context.SaveChangesAsync();

            // THÊM DÒNG NÀY: Sinh bút toán sổ cái sau khi ghi sổ thành công
            await _journalService.GenerateEntriesFromSalesVoucherAsync(voucher.Id);

            return Json(new { success = true });
        }
    }
}
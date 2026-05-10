using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.Services;
using RevenueAccountingMVC.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace RevenueAccountingMVC.Controllers
{
    /// <summary>
    /// Controller: Quản lý ghi sổ cái và sổ chi tiết
    /// </summary>
    public class LedgerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly JournalEntryService _journalService;
        private readonly ReportService _reportService;

        public LedgerController(
            ApplicationDbContext context,
            JournalEntryService journalService,
            ReportService reportService)
        {
            _context = context;
            _journalService = journalService;
            _reportService = reportService;
        }

        // ========== 1. GHI SỔ CÁI - XEM TẤT CẢ BÚT TOÁN ==========
        /// <summary>
        /// Hiển thị sổ cái (tất cả bút toán theo khoảng thời gian)
        /// </summary>
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> Index(
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string sortBy = "date")
        {
            // Set default dates (current month)
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = DateTime.Now.Date;

            var entries = await _journalService.GetEntriesByPeriodAsync(
                fromDate.Value,
                toDate.Value,
                accountId);

            // Sort
            entries = sortBy switch
            {
                "account" => entries.OrderBy(x => x.AccountId).ToList(),
                "voucher" => entries.OrderBy(x => x.VoucherCode).ToList(),
                _ => entries.OrderBy(x => x.PostingDate).ToList()
            };

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.AccountId = accountId;
            ViewBag.SortBy = sortBy;
            ViewBag.Accounts = await _context.Accounts
                .Where(x => x.Status == AccountStatus.Active)
                .OrderBy(x => x.AccountNumber)
                .ToListAsync();

            return View(entries);
        }

        // ========== 2. CHI TIẾT 1 CHỨNG TỪ ==========
        /// <summary>
        /// Xem tất cả bút toán của 1 chứng từ
        /// </summary>
        public async Task<IActionResult> VoucherDetail(int voucherId, string voucherType)
        {
            var entries = await _journalService.GetEntriesByVoucherAsync(voucherId, voucherType);

            if (!entries.Any())
            {
                return NotFound($"Không tìm thấy bút toán cho chứng từ {voucherType} ID {voucherId}");
            }

            ViewBag.VoucherCode = entries.FirstOrDefault()?.VoucherCode;
            ViewBag.VoucherType = voucherType;
            ViewBag.VoucherId = voucherId;

            return View(entries);
        }

        // ========== 3. CHI TIẾT 1 TÀI KHOẢN THEO KỲ (SỔ CHI TIẾT TK CÁI) ==========
        /// <summary>
        /// Sổ chi tiết của 1 tài khoản theo khoảng thời gian
        /// Hiển thị: Số dư đầu kỳ, Phát sinh Nợ, Phát sinh Có, Số dư cuối kỳ
        /// </summary>
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> AccountLedger(
            string? accountNumber,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var model = new ReportViewModel
            {
                AccountNumber = accountNumber?.Trim() ?? string.Empty,
                FromDate = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                ToDate = toDate ?? DateTime.Now.Date
            };

            ViewBag.HasSearched = false;

            if (string.IsNullOrWhiteSpace(accountNumber) && !fromDate.HasValue && !toDate.HasValue)
                return View(model);

            ViewBag.HasSearched = true;

            if (string.IsNullOrWhiteSpace(model.AccountNumber))
                ModelState.AddModelError(nameof(model.AccountNumber), "Vui lòng nhập mã tài khoản.");

            if (model.FromDate > model.ToDate)
                ModelState.AddModelError(nameof(model.ToDate), "Khoảng thời gian không hợp lệ.");

            if (!ModelState.IsValid)
                return View(model);

            var account = await _context.Accounts
                .FirstOrDefaultAsync(x => x.AccountNumber == model.AccountNumber);

            if (account == null)
            {
                ModelState.AddModelError(nameof(model.AccountNumber), "Không tìm thấy tài khoản.");
                return View(model);
            }

            model.AccountId = account.Id;
            model.AccountNumber = account.AccountNumber;

            var openingEntries = await _context.JournalEntries
                .Where(x => x.AccountId == account.Id && x.PostingDate < model.FromDate.Date)
                .ToListAsync();

            model.OpeningBalance = openingEntries
                .Sum(x => x.EntryType == "Debit" ? x.Amount : -x.Amount);

            // Xử lý lấy hết dữ liệu đến giây cuối cùng của ngày ToDate
            var endOfDay = model.ToDate.Date.AddDays(1).AddTicks(-1);

            var periodEntries = await _context.JournalEntries
                .Where(x => x.AccountId == account.Id
                         && x.PostingDate >= model.FromDate.Date
                         && x.PostingDate <= endOfDay)
                .OrderBy(x => x.PostingDate)
                .ThenBy(x => x.Id)
                .ToListAsync();

            if (!periodEntries.Any())
            {
                ViewBag.NoDataMessage = "Không có dữ liệu trong khoảng thời gian đã chọn";
                return View(model);
            }

            // --- BẮT ĐẦU FIX LỖI EF CORE TRANSLATION ---
            var voucherIds = periodEntries.Select(x => x.VoucherId).Distinct().ToList();
            var voucherTypes = periodEntries.Select(x => x.VoucherType).Distinct().ToList();

            var rawVoucherEntries = await _context.JournalEntries
                .Include(x => x.Account)
                .Where(x => voucherIds.Contains(x.VoucherId) && voucherTypes.Contains(x.VoucherType))
                .ToListAsync();

            var voucherKeys = periodEntries.Select(x => $"{x.VoucherType}:{x.VoucherId}").Distinct().ToList();

            var voucherEntries = rawVoucherEntries
                .Where(x => voucherKeys.Contains($"{x.VoucherType}:{x.VoucherId}"))
                .ToList();
            // --- KẾT THÚC FIX LỖI EF CORE TRANSLATION ---

            decimal runningBalance = model.OpeningBalance;

            foreach (var entry in periodEntries)
            {
                var debit = entry.EntryType == "Debit" ? entry.Amount : 0;
                var credit = entry.EntryType == "Credit" ? entry.Amount : 0;
                runningBalance += debit - credit;

                var correspondingEntry = voucherEntries
                    .Where(x => x.VoucherId == entry.VoucherId
                             && x.VoucherType == entry.VoucherType
                             && x.EntryType != entry.EntryType
                             && x.AccountId != entry.AccountId)
                    .OrderBy(x => x.Id)
                    .FirstOrDefault();

                model.Rows.Add(new ReportRowViewModel
                {
                    Date = entry.PostingDate,
                    Description = entry.Description ?? entry.VoucherCode,
                    CorrespondingAccount = correspondingEntry != null
                        ? $"{correspondingEntry.Account?.AccountNumber} - {correspondingEntry.Account?.AccountName}"
                        : "-",
                    Debit = debit,
                    Credit = credit,
                    Balance = runningBalance
                });
            }

            return View(model);
        }

        // ========== 4. GHI SỔ CHỨNG TỪ BÁN HÀNG ==========
        [HttpPost]
        public async Task<IActionResult> PostSalesVoucher(int voucherId)
        {
            try
            {
                var voucher = await _context.SalesVouchers.FindAsync(voucherId);
                if (voucher == null)
                    return NotFound("Không tìm thấy chứng từ");

                if (voucher.Status == VoucherStatus.Posted)
                    return BadRequest("Chứng từ đã được ghi sổ rồi");

                await _journalService.GenerateEntriesFromSalesVoucherAsync(voucherId);

                voucher.Status = VoucherStatus.Posted;
                _context.SalesVouchers.Update(voucher);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Ghi sổ chứng từ {voucher.VoucherCode} thành công!";
                return RedirectToAction("Index", "SalesVoucher");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi ghi sổ: {ex.Message}";
                return RedirectToAction("Index", "SalesVoucher");
            }
        }

        // ========== 5. GHI SỔ CHỨNG TỪ GIẢM GIÁ ==========
        [HttpPost]
        public async Task<IActionResult> PostRevenueAdjustment(int adjustmentId)
        {
            try
            {
                var adjustment = await _context.RevenueAdjustments.FindAsync(adjustmentId);
                if (adjustment == null)
                    return NotFound("Không tìm thấy chứng từ giảm giá");

                if (adjustment.Status == VoucherStatus.Posted)
                    return BadRequest("Chứng từ đã được ghi sổ rồi");

                await _journalService.GenerateEntriesFromRevenueAdjustmentAsync(adjustmentId);

                adjustment.Status = VoucherStatus.Posted;
                _context.RevenueAdjustments.Update(adjustment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Ghi sổ chứng từ {adjustment.AdjustmentCode} thành công!";
                return RedirectToAction("Index", "RevenueAdjustment");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi ghi sổ: {ex.Message}";
                return RedirectToAction("Index", "RevenueAdjustment");
            }
        }

        // ========== 6. HỦY GHI SỔ ==========
        [HttpPost]
        public async Task<IActionResult> UnpostVoucher(int voucherId, string voucherType)
        {
            try
            {
                await _journalService.RemoveEntriesByVoucherAsync(voucherId, voucherType);

                if (voucherType == "SalesVoucher")
                {
                    var voucher = await _context.SalesVouchers.FindAsync(voucherId);
                    if (voucher != null)
                    {
                        voucher.Status = VoucherStatus.Draft;
                        _context.SalesVouchers.Update(voucher);
                    }
                }
                else if (voucherType == "RevenueAdjustment")
                {
                    var adjustment = await _context.RevenueAdjustments.FindAsync(voucherId);
                    if (adjustment != null)
                    {
                        adjustment.Status = VoucherStatus.Draft;
                        _context.RevenueAdjustments.Update(adjustment);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Hủy ghi sổ thành công!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ========== 7. IN SỔ CÁI PDF ==========
        [HttpGet]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> PrintAccountLedger(string? accountNumber, DateTime? fromDate, DateTime? toDate, bool autoPrint = false)
        {
            var model = new ReportViewModel
            {
                AccountNumber = accountNumber?.Trim() ?? string.Empty,
                FromDate = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                ToDate = toDate ?? DateTime.Now.Date
            };

            if (string.IsNullOrWhiteSpace(model.AccountNumber) || model.FromDate > model.ToDate)
                return BadRequest("Dữ liệu tìm kiếm không hợp lệ.");

            var account = await _context.Accounts.FirstOrDefaultAsync(x => x.AccountNumber == model.AccountNumber);
            if (account == null) return NotFound("Không tìm thấy tài khoản.");

            model.AccountId = account.Id;
            model.AccountNumber = account.AccountNumber;

            var openingEntries = await _context.JournalEntries
                .Where(x => x.AccountId == account.Id && x.PostingDate < model.FromDate.Date)
                .ToListAsync();

            model.OpeningBalance = openingEntries.Sum(x => x.EntryType == "Debit" ? x.Amount : -x.Amount);

            var endOfDay = model.ToDate.Date.AddDays(1).AddTicks(-1);

            var periodEntries = await _context.JournalEntries
                .Where(x => x.AccountId == account.Id
                        && x.PostingDate >= model.FromDate.Date
                        && x.PostingDate <= endOfDay)
                .OrderBy(x => x.PostingDate).ThenBy(x => x.Id)
                .ToListAsync();

            var voucherIds = periodEntries.Select(x => x.VoucherId).Distinct().ToList();
            var voucherTypes = periodEntries.Select(x => x.VoucherType).Distinct().ToList();

            var rawVoucherEntries = await _context.JournalEntries
                .Include(x => x.Account)
                .Where(x => voucherIds.Contains(x.VoucherId) && voucherTypes.Contains(x.VoucherType))
                .ToListAsync();

            var voucherKeys = periodEntries.Select(x => $"{x.VoucherType}:{x.VoucherId}").Distinct().ToList();

            var voucherEntries = rawVoucherEntries
                .Where(x => voucherKeys.Contains($"{x.VoucherType}:{x.VoucherId}"))
                .ToList();

            decimal runningBalance = model.OpeningBalance;

            foreach (var entry in periodEntries)
            {
                var debit = entry.EntryType == "Debit" ? entry.Amount : 0;
                var credit = entry.EntryType == "Credit" ? entry.Amount : 0;
                runningBalance += debit - credit;

                var correspondingEntry = voucherEntries
                    .Where(x => x.VoucherId == entry.VoucherId
                            && x.VoucherType == entry.VoucherType
                            && x.EntryType != entry.EntryType
                            && x.AccountId != entry.AccountId)
                    .OrderBy(x => x.Id).FirstOrDefault();

                model.Rows.Add(new ReportRowViewModel
                {
                    Date = entry.PostingDate,
                    Description = entry.Description ?? entry.VoucherCode,
                    VoucherCode = entry.VoucherCode,
                    CorrespondingAccount = correspondingEntry?.Account?.AccountNumber ?? "-",
                    Debit = debit,
                    Credit = credit,
                    Balance = runningBalance
                });
            }

            // Truyền cờ autoPrint sang View
            ViewBag.AutoPrint = autoPrint;

            return View(model);
        }

        // ========== 8. CHI TIẾT MỘT BÚT TOÁN (AJAX) ==========
        /// <summary>
        /// Trả về chi tiết một bút toán dưới dạng HTML partial
        /// Dùng cho modal hiển thị chi tiết
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> EntryDetail(int id)
        {
            var entry = await _context.JournalEntries
                .Include(x => x.Account)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entry == null)
                return NotFound();

            // Trả về partial view
            return PartialView("_EntryDetailPartial", entry);
        }

        // ========== 9. BÁO CÁO DOANH THU THEO KHÁCH HÀNG ==========
        /// <summary>
        /// Hiển thị báo cáo doanh thu theo khách hàng (Luồng 1-7)
        /// </summary>
        public async Task<IActionResult> RevenueByCustomer(DateTime? fromDate, DateTime? toDate, int? customerId)
        {
            // A2: Kiểm tra điều kiện bắt buộc (Từ ngày, Đến ngày)
            if (!fromDate.HasValue || !toDate.HasValue)
            {
                // Lần đầu tiên truy cập: không có tìm kiếm
                ViewBag.HasSearched = false;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.Customers = await _context.Customers
                    .Where(x => x.Status == CustomerStatus.Active)
                    .OrderBy(x => x.CustomerCode)
                    .ToListAsync();
                return View(new List<RevenueByCustomerReportLine>());
            }

            // Kiểm tra tính hợp lệ của khoảng thời gian
            if (fromDate.Value > toDate.Value)
            {
                ModelState.AddModelError("ToDate", "Ngày kết thúc phải >= ngày bắt đầu");
                ViewBag.HasSearched = false;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.Customers = await _context.Customers
                    .Where(x => x.Status == CustomerStatus.Active)
                    .OrderBy(x => x.CustomerCode)
                    .ToListAsync();
                return View(new List<RevenueByCustomerReportLine>());
            }

            ViewBag.HasSearched = true;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.SelectedCustomerId = customerId;

            // Bước 5: Truy xuất dữ liệu
            // Bước 6: Tổng hợp doanh thu theo khách hàng
            var reportLines = await _reportService.GenerateRevenueByCustomerReportAsync(
                fromDate.Value,
                toDate.Value);

            // Lọc theo khách hàng nếu có chọn
            if (customerId.HasValue && customerId.Value > 0)
            {
                reportLines = reportLines.Where(x => x.CustomerId == customerId.Value).ToList();
            }

            // Bước 7: Hiển thị danh sách
            ViewBag.Customers = await _context.Customers
                .Where(x => x.Status == CustomerStatus.Active)
                .OrderBy(x => x.CustomerCode)
                .ToListAsync();

            return View(reportLines);
        }

        // ========== 10. CHI TIẾT DOANH THU KHÁCH HÀNG (A3) ==========
        /// <summary>
        /// Hiển thị chi tiết chứng từ của 1 khách hàng
        /// </summary>
        public async Task<IActionResult> CustomerRevenueDetail(
            int customerId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!fromDate.HasValue || !toDate.HasValue)
                return BadRequest("Vui lòng chọn khoảng thời gian");

            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return NotFound("Không tìm thấy khách hàng");

            // Lấy tất cả chứng từ bán hàng của khách hàng
            var salesVouchers = await _context.SalesVouchers
                .Include(x => x.Customer)
                .Include(x => x.Details)
                .ThenInclude(x => x.Product)
                .Where(x => x.CustomerId == customerId
                         && x.AccountingDate >= fromDate.Value
                         && x.AccountingDate <= toDate.Value
                         && x.Status == VoucherStatus.Posted)
                .OrderByDescending(x => x.AccountingDate)
                .ToListAsync();

            // Lấy tất cả chứng từ giảm giá của khách hàng
            var adjustments = await _context.RevenueAdjustments
                .Include(x => x.Customer)
                .Include(x => x.Details)
                .ThenInclude(x => x.Product)
                .Where(x => x.CustomerId == customerId
                         && x.AccountingDate >= fromDate.Value
                         && x.AccountingDate <= toDate.Value
                         && x.Status == VoucherStatus.Posted)
                .OrderByDescending(x => x.AccountingDate)
                .ToListAsync();

            ViewBag.Customer = customer;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.SalesVouchers = salesVouchers;
            ViewBag.Adjustments = adjustments;

            // Tính tổng
            var totalRevenue = salesVouchers.SelectMany(x => x.Details).Sum(x => x.Amount);
            var totalTax = salesVouchers.SelectMany(x => x.Details).Sum(x => x.TaxAmount);
            var adjustmentAmount = adjustments.SelectMany(x => x.Details).Sum(x => x.Amount);

            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalTax = totalTax;
            ViewBag.AdjustmentAmount = adjustmentAmount;
            ViewBag.NetRevenue = totalRevenue + adjustmentAmount;

            return View();
        }

        // ========== 11. BÁO CÁO DOANH THU THEO MẶT HÀNG ==========
        /// <summary>
        /// Hiển thị báo cáo doanh thu theo mặt hàng trên Web
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RevenueByProduct(DateTime? fromDate, DateTime? toDate, int? productId)
        {
            if (!fromDate.HasValue || !toDate.HasValue)
            {
                ViewBag.HasSearched = false;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.Products = await _context.Products
                    .Where(x => x.Status == ProductStatus.Active)
                    .OrderBy(x => x.ProductCode)
                    .ToListAsync();
                return View(new List<RevenueByProductReportLine>());
            }

            if (fromDate.Value > toDate.Value)
            {
                ModelState.AddModelError("ToDate", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
                ViewBag.HasSearched = false;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.Products = await _context.Products
                    .Where(x => x.Status == ProductStatus.Active)
                    .OrderBy(x => x.ProductCode)
                    .ToListAsync();
                return View(new List<RevenueByProductReportLine>());
            }

            ViewBag.HasSearched = true;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.SelectedProductId = productId;

            // Truy xuất dữ liệu từ Service (Đã group by theo mặt hàng)
            var reportLines = await _reportService.GenerateRevenueByProductReportAsync(
                fromDate.Value, 
                toDate.Value);

            // Lọc thêm theo mã sản phẩm nếu người dùng có chọn
            if (productId.HasValue && productId.Value > 0)
            {
                reportLines = reportLines.Where(x => x.ProductId == productId.Value).ToList();
            }

            ViewBag.Products = await _context.Products
                .Where(x => x.Status == ProductStatus.Active)
                .OrderBy(x => x.ProductCode)
                .ToListAsync();

            return View(reportLines);
        }

        // ========== 12. IN BÁO CÁO DOANH THU THEO MẶT HÀNG (PDF) ==========
        /// <summary>
        /// Màn hình in báo cáo theo chuẩn mẫu
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PrintRevenueByProduct(DateTime fromDate, DateTime toDate, int? productId, bool autoPrint = false)
        {
            var reportLines = await _reportService.GenerateRevenueByProductReportAsync(fromDate, toDate);

            if (productId.HasValue && productId.Value > 0)
            {
                reportLines = reportLines.Where(x => x.ProductId == productId.Value).ToList();
            }

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            
            // Truyền cờ in sang View
            ViewBag.AutoPrint = autoPrint;

            return View(reportLines);
        }

        // ========== 13. BÁO CÁO DOANH THU TỔNG HỢP ==========
        /// <summary>
        /// Hiển thị báo cáo doanh thu tổng hợp trên Web
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RevenueSummary(DateTime? fromDate, DateTime? toDate, int? productId, int? customerId)
        {
            // Load Dropdowns
            ViewBag.Products = await _context.Products.Where(x => x.Status == ProductStatus.Active).OrderBy(x => x.ProductCode).ToListAsync();
            ViewBag.Customers = await _context.Customers.Where(x => x.Status == CustomerStatus.Active).OrderBy(x => x.CustomerCode).ToListAsync();

            if (!fromDate.HasValue || !toDate.HasValue)
            {
                ViewBag.HasSearched = false;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                return View(new List<RevenueByCustomerReportLine>());
            }

            if (fromDate.Value > toDate.Value)
            {
                ModelState.AddModelError("ToDate", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
                ViewBag.HasSearched = false;
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                return View(new List<RevenueByCustomerReportLine>());
            }

            ViewBag.HasSearched = true;
            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.SelectedProductId = productId;
            ViewBag.SelectedCustomerId = customerId;

            // Truy xuất dữ liệu (Gộp theo khách hàng để hiển thị lên bảng tổng hợp giống mẫu PDF)
            // Lưu ý: Nếu Service của bạn có hàm GenerateRevenueSummaryAsync hỗ trợ lọc productId thì dùng hàm đó.
            // Ở đây tạm gọi lại hàm GenerateRevenueByCustomerReportAsync.
            var reportLines = await _reportService.GenerateRevenueByCustomerReportAsync(fromDate.Value, toDate.Value);

            // Lọc theo khách hàng trên RAM
            if (customerId.HasValue && customerId.Value > 0)
            {
                reportLines = reportLines.Where(x => x.CustomerId == customerId.Value).ToList();
            }

            return View(reportLines);
        }

        // ========== 14. IN BÁO CÁO DOANH THU TỔNG HỢP (PDF) ==========
        [HttpGet]
        public async Task<IActionResult> PrintRevenueSummary(DateTime fromDate, DateTime toDate, int? productId, int? customerId, bool autoPrint = false)
        {
            var reportLines = await _reportService.GenerateRevenueByCustomerReportAsync(fromDate, toDate);

            if (customerId.HasValue && customerId.Value > 0)
            {
                reportLines = reportLines.Where(x => x.CustomerId == customerId.Value).ToList();
            }

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            
            // Truyền cờ in sang View
            ViewBag.AutoPrint = autoPrint;

            return View(reportLines);
        }

        // ========== 15. IN SỔ NHẬT KÝ CHUNG (PDF) ==========
        /// <summary>
        /// Màn hình in Sổ Nhật Ký Chung theo Mẫu số S03a-DN
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Accountant")]
        public async Task<IActionResult> PrintJournal(DateTime? fromDate, DateTime? toDate, int? accountId, string sortBy = "date", bool autoPrint = false)
        {
            if (!fromDate.HasValue) 
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue) 
                toDate = DateTime.Now.Date;

            // Tái sử dụng logic lấy dữ liệu giống hàm Index
            var entries = await _journalService.GetEntriesByPeriodAsync(fromDate.Value, toDate.Value, accountId);

            entries = sortBy switch
            {
                "account" => entries.OrderBy(x => x.AccountId).ToList(),
                "voucher" => entries.OrderBy(x => x.VoucherCode).ToList(),
                _ => entries.OrderBy(x => x.PostingDate).ToList()
            };

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            
            // Truyền cờ in sang View
            ViewBag.AutoPrint = autoPrint;

            return View(entries);
        }

        // ========== IN BÁO CÁO DOANH THU THEO KHÁCH HÀNG (PDF) ==========
        [HttpGet]
        public async Task<IActionResult> PrintRevenueByCustomer(DateTime fromDate, DateTime toDate, int? customerId, bool autoPrint = false)
        {
            var reportLines = await _reportService.GenerateRevenueByCustomerReportAsync(fromDate, toDate);

            if (customerId.HasValue && customerId.Value > 0)
            {
                reportLines = reportLines.Where(x => x.CustomerId == customerId.Value).ToList();
            }

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            
            // Truyền cờ in sang View
            ViewBag.AutoPrint = autoPrint;

            return View(reportLines);
        }
    }
}
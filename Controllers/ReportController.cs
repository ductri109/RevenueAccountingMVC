using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.Services;

namespace RevenueAccountingMVC.Controllers
{
    /// <summary>
    /// Controller: Báo cáo doanh thu
    /// </summary>
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ReportService _reportService;

        public ReportController(ApplicationDbContext context, ReportService reportService)
        {
            _context = context;
            _reportService = reportService;
        }

        // ========== 1. BÁOO CÁO DOANH THU TỔNG HỢP ==========
        /// <summary>
        /// Báo cáo doanh thu tổng hợp theo sản phẩm
        /// </summary>
        public async Task<IActionResult> RevenueReport(
            DateTime? fromDate,
            DateTime? toDate)
        {
            // Set default dates (current month)
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = DateTime.Now.Date;

            var report = await _reportService.GenerateRevenueReportAsync(fromDate.Value, toDate.Value);

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.TotalRevenue = report.Sum(x => x.TotalRevenue);
            ViewBag.TotalTax = report.Sum(x => x.TotalTax);
            ViewBag.TotalPayment = report.Sum(x => x.TotalPayment);
            ViewBag.TotalAdjustment = report.Sum(x => x.AdjustmentRevenue);
            ViewBag.NetRevenue = report.Sum(x => x.NetRevenue);

            return View(report);
        }

        // ========== 2. BÁOO CÁO DOANH THU THEO KHÁCH HÀNG ==========
        /// <summary>
        /// Báo cáo doanh thu theo khách hàng
        /// </summary>
        public async Task<IActionResult> RevenueByCustomerReport(
            DateTime? fromDate,
            DateTime? toDate,
            string sortBy = "revenue")
        {
            // Set default dates
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = DateTime.Now.Date;

            var report = await _reportService.GenerateRevenueByCustomerReportAsync(fromDate.Value, toDate.Value);

            // Sort
            report = sortBy switch
            {
                "name" => report.OrderBy(x => x.CustomerName).ToList(),
                "count" => report.OrderByDescending(x => x.TransactionCount).ToList(),
                _ => report.OrderByDescending(x => x.TotalPayment).ToList()
            };

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.SortBy = sortBy;
            ViewBag.TotalRevenue = report.Sum(x => x.TotalRevenue);
            ViewBag.TotalPayment = report.Sum(x => x.TotalPayment);

            return View(report);
        }

        // ========== 3. BÁOO CÁO DOANH THU THEO SẢN PHẨM ==========
        /// <summary>
        /// Báo cáo doanh thu theo sản phẩm
        /// </summary>
        public async Task<IActionResult> RevenueByProductReport(
            DateTime? fromDate,
            DateTime? toDate,
            string sortBy = "revenue")
        {
            // Set default dates
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = DateTime.Now.Date;

            var report = await _reportService.GenerateRevenueByProductReportAsync(fromDate.Value, toDate.Value);

            // Sort
            report = sortBy switch
            {
                "quantity" => report.OrderByDescending(x => x.Quantity).ToList(),
                "name" => report.OrderBy(x => x.ProductName).ToList(),
                _ => report.OrderByDescending(x => x.TotalRevenue).ToList()
            };

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.SortBy = sortBy;
            ViewBag.TotalQuantity = report.Sum(x => x.Quantity);
            ViewBag.TotalRevenue = report.Sum(x => x.TotalRevenue);
            ViewBag.TotalPayment = report.Sum(x => x.TotalPayment);

            return View(report);
        }

        // ========== 4. SỔ CÁI (GENERAL LEDGER) ==========
        /// <summary>
        /// Sổ cái tổng hợp - Xem số dư nợ/có của từng TK
        /// </summary>
        public async Task<IActionResult> GeneralLedger(
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId)
        {
            // Set default dates
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = DateTime.Now.Date;

            var ledger = await _reportService.GenerateGeneralLedgerAsync(
                fromDate.Value,
                toDate.Value,
                accountId);

            ViewBag.FromDate = fromDate;
            ViewBag.ToDate = toDate;
            ViewBag.AccountId = accountId;
            ViewBag.Accounts = await _context.Accounts
                .Where(x => x.Status == AccountStatus.Active)
                .OrderBy(x => x.AccountNumber)
                .ToListAsync();

            return View(ledger);
        }

        // ========== 5. XUẤT EXCEL ==========
        /// <summary>
        /// Xuất báo cáo doanh thu sang Excel
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ExportRevenueReportToExcel(
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (!fromDate.HasValue)
                fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue)
                toDate = DateTime.Now.Date;

            var report = await _reportService.GenerateRevenueReportAsync(fromDate.Value, toDate.Value);

            // TODO: Sử dụng EPPlus hoặc ClosedXML để tạo file Excel
            // Tạm thời chỉ return JSON
            return Json(new { message = "Chức năng xuất Excel sẽ được implement sau" });
        }

        // ========== 6. TRANG CHỦ REPORT ==========
        /// <summary>
        /// Trang chủ báo cáo - Menu chọn loại báo cáo
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }
    }
}

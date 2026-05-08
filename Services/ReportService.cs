using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.Data;

namespace RevenueAccountingMVC.Services
{
    /// <summary>
    /// Service tạo báo cáo doanh thu
    /// </summary>
    public class ReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Báo cáo doanh thu tổng hợp theo sản phẩm
        /// </summary>
        public async Task<List<RevenueReportLine>> GenerateRevenueReportAsync(
            DateTime fromDate,
            DateTime toDate)
        {
            var result = new List<RevenueReportLine>();

            // ========== 1. LẤY DỮ LIỆU BÁN HÀNG ==========
            var salesDetails = await _context.SalesVoucherDetails
                .Include(x => x.Product)
                .Include(x => x.SalesVoucher)
                .Where(x => x.SalesVoucher.AccountingDate >= fromDate
                         && x.SalesVoucher.AccountingDate <= toDate
                         && x.SalesVoucher.Status == VoucherStatus.Posted)
                .ToListAsync();

            // Nhóm theo sản phẩm
            var salesByProduct = salesDetails
                .GroupBy(x => new { x.ProductId, x.Product })
                .Select(g => new RevenueReportLine
                {
                    PeriodStart = fromDate,
                    PeriodEnd = toDate,
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Product?.ProductName ?? "N/A",
                    ProductCode = g.Key.Product?.ProductCode,
                    Unit = g.FirstOrDefault()?.UnitSnapshot ?? g.Key.Product?.Unit,
                    Quantity = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Amount),
                    TotalTax = g.Sum(x => x.TaxAmount),
                    TotalPayment = g.Sum(x => x.Amount + x.TaxAmount),
                    CustomerCount = g.Select(x => x.SalesVoucher.CustomerId).Distinct().Count()
                })
                .ToList();

            result.AddRange(salesByProduct);

            // ========== 2. LẤY DỮ LIỆU GIẢM GIÁ ==========
            var adjustmentDetails = await _context.RevenueAdjustmentDetails
                .Include(x => x.Product)
                .Include(x => x.RevenueAdjustment)
                .Where(x => x.RevenueAdjustment.AccountingDate >= fromDate
                         && x.RevenueAdjustment.AccountingDate <= toDate
                         && x.RevenueAdjustment.Status == VoucherStatus.Posted)
                .ToListAsync();

            var adjustmentByProduct = adjustmentDetails
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AdjustmentRevenue = g.Sum(x => x.Amount),  // Âm
                    AdjustmentTax = g.Sum(x => x.TaxAmount)    // Âm
                })
                .ToList();

            // Merge với dữ liệu bán
            foreach (var adj in adjustmentByProduct)
            {
                var existing = result.FirstOrDefault(x => x.ProductId == adj.ProductId);
                if (existing != null)
                {
                    existing.AdjustmentRevenue = adj.AdjustmentRevenue;
                    existing.AdjustmentQuantity = adjustmentDetails
                        .Where(x => x.ProductId == adj.ProductId)
                        .Sum(x => x.Quantity);
                }
                else
                {
                    // Trường hợp chỉ có giảm giá, không có bán
                    var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == adj.ProductId);
                    result.Add(new RevenueReportLine
                    {
                        PeriodStart = fromDate,
                        PeriodEnd = toDate,
                        ProductId = adj.ProductId,
                        ProductName = product?.ProductName ?? "N/A",
                        ProductCode = product?.ProductCode,
                        AdjustmentRevenue = adj.AdjustmentRevenue,
                        AdjustmentTax = adj.AdjustmentTax
                    });
                }
            }

            // Tính giá bình quân
            foreach (var line in result)
            {
                if (line.Quantity > 0)
                    line.UnitPrice = Math.Round(line.TotalRevenue / line.Quantity, 2);
                line.GeneratedAt = DateTime.Now;
            }

            return result.OrderByDescending(x => x.TotalRevenue).ToList();
        }

        /// <summary>
        /// Báo cáo doanh thu theo khách hàng
        /// </summary>
        public async Task<List<RevenueByCustomerReportLine>> GenerateRevenueByCustomerReportAsync(
            DateTime fromDate,
            DateTime toDate)
        {
            var result = new List<RevenueByCustomerReportLine>();

            // ========== 1. LẤY DỮ LIỆU BÁN HÀNG ==========
            var salesVouchers = await _context.SalesVouchers
                .Include(x => x.Customer)
                .Include(x => x.Details)
                .Where(x => x.AccountingDate >= fromDate
                         && x.AccountingDate <= toDate
                         && x.Status == VoucherStatus.Posted)
                .ToListAsync();

            var salesByCustomer = salesVouchers
                .GroupBy(x => new { x.CustomerId, x.Customer })
                .Select(g => new RevenueByCustomerReportLine
                {
                    PeriodStart = fromDate,
                    PeriodEnd = toDate,
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.Customer?.CustomerName ?? "N/A",
                    CustomerCode = g.Key.Customer?.CustomerCode,
                    TaxCode = g.Key.Customer?.TaxCode,
                    TransactionCount = g.Count(),
                    TotalRevenue = g.SelectMany(x => x.Details).Sum(x => x.Amount),
                    TotalTax = g.SelectMany(x => x.Details).Sum(x => x.TaxAmount),
                    TotalPayment = g.SelectMany(x => x.Details).Sum(x => x.Amount + x.TaxAmount),
                    AverageDebtDays = g.Average(x => x.DebtDays) > 0 ? (int)g.Average(x => x.DebtDays) : 0
                })
                .ToList();

            result.AddRange(salesByCustomer);

            // ========== 2. LẤY DỮ LIỆU GIẢM GIÁ ==========
            var adjustments = await _context.RevenueAdjustments
                .Include(x => x.Customer)
                .Where(x => x.AccountingDate >= fromDate
                         && x.AccountingDate <= toDate
                         && x.Status == VoucherStatus.Posted)
                .ToListAsync();

            var adjustmentByCustomer = adjustments
                .GroupBy(x => x.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    AdjustmentRevenue = g.Sum(x => x.TotalDiscountAmount + x.TotalTaxAmount)  // Âm
                })
                .ToList();

            // Merge
            foreach (var adj in adjustmentByCustomer)
            {
                var existing = result.FirstOrDefault(x => x.CustomerId == adj.CustomerId);
                if (existing != null)
                {
                    existing.AdjustmentRevenue = adj.AdjustmentRevenue;
                }
            }

            // Tính giá trị trung bình mỗi giao dịch
            foreach (var line in result)
            {
                if (line.TransactionCount > 0)
                    line.AverageOrderValue = Math.Round(line.TotalPayment / line.TransactionCount, 2);
                line.GeneratedAt = DateTime.Now;
            }

            return result.OrderByDescending(x => x.TotalPayment).ToList();
        }

        /// <summary>
        /// Báo cáo doanh thu theo sản phẩm
        /// </summary>
        public async Task<List<RevenueByProductReportLine>> GenerateRevenueByProductReportAsync(
            DateTime fromDate,
            DateTime toDate)
        {
            var result = new List<RevenueByProductReportLine>();

            // ========== 1. LẤY DỮ LIỆU BÁN HÀNG ==========
            var salesDetails = await _context.SalesVoucherDetails
                .Include(x => x.Product)
                .Include(x => x.SalesVoucher)
                .Where(x => x.SalesVoucher.AccountingDate >= fromDate
                         && x.SalesVoucher.AccountingDate <= toDate
                         && x.SalesVoucher.Status == VoucherStatus.Posted)
                .ToListAsync();

            var salesByProduct = salesDetails
                .GroupBy(x => new { x.ProductId, x.Product })
                .Select(g => new RevenueByProductReportLine
                {
                    PeriodStart = fromDate,
                    PeriodEnd = toDate,
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Product?.ProductName ?? "N/A",
                    ProductCode = g.Key.Product?.ProductCode,
                    Unit = g.Key.Product?.Unit,
                    Quantity = g.Sum(x => x.Quantity),
                    AverageUnitPrice = g.Count() > 0 ? g.Sum(x => x.Amount) / g.Sum(x => x.Quantity) : 0,
                    TotalRevenue = g.Sum(x => x.Amount),
                    TotalTax = g.Sum(x => x.TaxAmount),
                    TotalPayment = g.Sum(x => x.Amount + x.TaxAmount),
                    CustomerCount = g.Select(x => x.SalesVoucher.CustomerId).Distinct().Count()
                })
                .ToList();

            result.AddRange(salesByProduct);

            // ========== 2. LẤY DỮ LIỆU GIẢM GIÁ ==========
            var adjustmentDetails = await _context.RevenueAdjustmentDetails
                .Include(x => x.Product)
                .Include(x => x.RevenueAdjustment)
                .Where(x => x.RevenueAdjustment.AccountingDate >= fromDate
                         && x.RevenueAdjustment.AccountingDate <= toDate
                         && x.RevenueAdjustment.Status == VoucherStatus.Posted)
                .ToListAsync();

            var adjustmentByProduct = adjustmentDetails
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    AdjustmentQuantity = g.Sum(x => x.Quantity),  // Âm
                    AdjustmentRevenue = g.Sum(x => x.Amount)      // Âm
                })
                .ToList();

            // Merge
            foreach (var adj in adjustmentByProduct)
            {
                var existing = result.FirstOrDefault(x => x.ProductId == adj.ProductId);
                if (existing != null)
                {
                    existing.AdjustmentQuantity = adj.AdjustmentQuantity;
                    existing.AdjustmentRevenue = adj.AdjustmentRevenue;
                }
            }

            foreach (var line in result)
                line.GeneratedAt = DateTime.Now;

            return result.OrderByDescending(x => x.TotalRevenue).ToList();
        }

        /// <summary>
        /// Lấy dữ liệu sổ cái tổng hợp theo TK
        /// </summary>
        public async Task<List<dynamic>> GenerateGeneralLedgerAsync(
            DateTime fromDate,
            DateTime toDate,
            int? accountId = null)
        {
            var result = new List<dynamic>();

            var query = _context.JournalEntries
                .Include(x => x.Account)
                .Where(x => x.PostingDate >= fromDate && x.PostingDate <= toDate);

            if (accountId.HasValue)
                query = query.Where(x => x.AccountId == accountId);

            var entries = await query.ToListAsync();

            var accounts = entries
                .Select(x => x.AccountId)
                .Distinct()
                .ToList();

            foreach (var accId in accounts)
            {
                var accEntries = entries.Where(x => x.AccountId == accId).ToList();
                var account = accEntries.FirstOrDefault()?.Account;

                var totalDebit = accEntries.Where(x => x.EntryType == "Debit").Sum(x => x.Amount);
                var totalCredit = accEntries.Where(x => x.EntryType == "Credit").Sum(x => x.Amount);

                result.Add(new
                {
                    AccountId = accId,
                    AccountNumber = account?.AccountNumber,
                    AccountName = account?.AccountName,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    Balance = totalDebit - totalCredit,
                    Entries = accEntries.OrderBy(x => x.PostingDate).ToList()
                });
            }

            return result.OrderBy(x => ((dynamic)x).AccountNumber).ToList();
        }
    }
}

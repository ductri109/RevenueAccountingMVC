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
    /// Service xử lý bút toán (JournalEntry)
    /// </summary>
    public class JournalEntryService
    {
        private readonly ApplicationDbContext _context;

        public JournalEntryService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Sinh bút toán từ SalesVoucher
        /// </summary>
        public async Task GenerateEntriesFromSalesVoucherAsync(int voucherId)
        {
            var voucher = await _context.SalesVouchers
                .Include(x => x.Customer)
                .Include(x => x.Details)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == voucherId);

            if (voucher == null)
                throw new Exception($"Không tìm thấy chứng từ bán hàng ID {voucherId}");

            // Xóa bút toán cũ nếu có (để tránh duplicate khi re-post)
            var oldEntries = await _context.JournalEntries
                .Where(x => x.VoucherId == voucherId && x.VoucherType == "SalesVoucher")
                .ToListAsync();
            if (oldEntries.Count > 0)
                _context.JournalEntries.RemoveRange(oldEntries);

            foreach (var detail in voucher.Details)
            {
                // ========== BÚT TOÁN HÀNG BÁN ==========
                // Nợ TK Tài sản (131 - Hàng bán lẻ) = Thành tiền
                // Có TK Doanh thu (511 - Doanh thu bán hàng) = Thành tiền

                await CreateEntryAsync(new JournalEntry
                {
                    VoucherType = "SalesVoucher",
                    VoucherId = voucher.Id,
                    VoucherCode = voucher.VoucherCode,
                    VoucherDate = voucher.AccountingDate,
                    AccountId = detail.DebitAccountId ?? 131,  // Default: 131 (Hàng bán lẻ)
                    EntryType = "Debit",
                    Amount = detail.Amount,
                    CustomerId = voucher.CustomerId,
                    CustomerName = voucher.CustomerNameSnapshot ?? voucher.Customer?.CustomerName,
                    CustomerCode = voucher.Customer?.CustomerCode,
                    ProductId = detail.ProductId,
                    ProductName = detail.ProductNameSnapshot ?? detail.Product?.ProductName,
                    ProductCode = detail.Product?.ProductCode,
                    Description = $"Bán hàng {voucher.VoucherCode}",
                    PostingDate = voucher.AccountingDate,
                    CreatedBy = "System"
                });

                await CreateEntryAsync(new JournalEntry
                {
                    VoucherType = "SalesVoucher",
                    VoucherId = voucher.Id,
                    VoucherCode = voucher.VoucherCode,
                    VoucherDate = voucher.AccountingDate,
                    AccountId = detail.CreditAccountId ?? 511,  // Default: 511 (Doanh thu bán hàng)
                    EntryType = "Credit",
                    Amount = detail.Amount,
                    CustomerId = voucher.CustomerId,
                    CustomerName = voucher.CustomerNameSnapshot ?? voucher.Customer?.CustomerName,
                    CustomerCode = voucher.Customer?.CustomerCode,
                    ProductId = detail.ProductId,
                    ProductName = detail.ProductNameSnapshot ?? detail.Product?.ProductName,
                    ProductCode = detail.Product?.ProductCode,
                    Description = $"Bán hàng {voucher.VoucherCode}",
                    PostingDate = voucher.AccountingDate,
                    CreatedBy = "System"
                });

                // ========== BÚT TOÁN THUẾ ==========
                // Nợ TK 333 (Thuế GTGT phải nộp) = Tiền thuế
                // Có TK 3333 (Thuế GTGT phải nộp chi tiết) = Tiền thuế

                if (detail.TaxAmount > 0)
                {
                    await CreateEntryAsync(new JournalEntry
                    {
                        VoucherType = "SalesVoucher",
                        VoucherId = voucher.Id,
                        VoucherCode = voucher.VoucherCode,
                        VoucherDate = voucher.AccountingDate,
                        AccountId = 333,  // Thuế GTGT phải nộp
                        EntryType = "Debit",
                        Amount = detail.TaxAmount,
                        CustomerId = voucher.CustomerId,
                        ProductId = detail.ProductId,
                        TaxId = detail.TaxId,
                        TaxRate = detail.TaxRateSnapshot,
                        Description = $"Thuế bán hàng {voucher.VoucherCode}",
                        PostingDate = voucher.AccountingDate,
                        CreatedBy = "System"
                    });

                    await CreateEntryAsync(new JournalEntry
                    {
                        VoucherType = "SalesVoucher",
                        VoucherId = voucher.Id,
                        VoucherCode = voucher.VoucherCode,
                        VoucherDate = voucher.AccountingDate,
                        AccountId = detail.TaxAccountId ?? 3333,  // Thuế GTGT chi tiết
                        EntryType = "Credit",
                        Amount = detail.TaxAmount,
                        CustomerId = voucher.CustomerId,
                        ProductId = detail.ProductId,
                        TaxId = detail.TaxId,
                        TaxRate = detail.TaxRateSnapshot,
                        Description = $"Thuế bán hàng {voucher.VoucherCode}",
                        PostingDate = voucher.AccountingDate,
                        CreatedBy = "System"
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Sinh bút toán từ RevenueAdjustment (bút toán ĐẢO NGƯỢC)
        /// </summary>
        public async Task GenerateEntriesFromRevenueAdjustmentAsync(int adjustmentId)
        {
            var adjustment = await _context.RevenueAdjustments
                .Include(x => x.Customer)
                .Include(x => x.Details)
                .ThenInclude(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == adjustmentId);

            if (adjustment == null)
                throw new Exception($"Không tìm thấy chứng từ giảm giá ID {adjustmentId}");

            // Xóa bút toán cũ nếu có
            var oldEntries = await _context.JournalEntries
                .Where(x => x.VoucherId == adjustmentId && x.VoucherType == "RevenueAdjustment")
                .ToListAsync();
            if (oldEntries.Count > 0)
                _context.JournalEntries.RemoveRange(oldEntries);

            foreach (var detail in adjustment.Details)
            {
                // ========== BÚT TOÁN HÀNG GIẢM (ĐẢO NGƯỢC) ==========
                // Khi bán: Nợ TK 131, Có TK 511
                // Khi giảm: Nợ TK 511 (lợi nhuận giảm), Có TK 131 (hàng trả về)
                // Nhưng Amount của adjustment.Details là ÂM, nên phải lấy Math.Abs()

                var absAmount = Math.Abs(detail.Amount);

                await CreateEntryAsync(new JournalEntry
                {
                    VoucherType = "RevenueAdjustment",
                    VoucherId = adjustment.Id,
                    VoucherCode = adjustment.AdjustmentCode,
                    VoucherDate = adjustment.AdjustmentDate,
                    AccountId = detail.CreditAccountId ?? 511,  // Doanh thu (bây giờ là nợ)
                    EntryType = "Debit",
                    Amount = absAmount,
                    CustomerId = adjustment.CustomerId,
                    CustomerName = adjustment.Customer?.CustomerName,
                    CustomerCode = adjustment.Customer?.CustomerCode,
                    ProductId = detail.ProductId,
                    ProductName = detail.Product?.ProductName,
                    ProductCode = detail.Product?.ProductCode,
                    Description = $"Giảm {detail.AdjustmentType} {adjustment.AdjustmentCode}",
                    PostingDate = adjustment.AccountingDate,
                    CreatedBy = "System"
                });

                await CreateEntryAsync(new JournalEntry
                {
                    VoucherType = "RevenueAdjustment",
                    VoucherId = adjustment.Id,
                    VoucherCode = adjustment.AdjustmentCode,
                    VoucherDate = adjustment.AdjustmentDate,
                    AccountId = detail.DebitAccountId ?? 131,  // Hàng (bây giờ là có)
                    EntryType = "Credit",
                    Amount = absAmount,
                    CustomerId = adjustment.CustomerId,
                    CustomerName = adjustment.Customer?.CustomerName,
                    CustomerCode = adjustment.Customer?.CustomerCode,
                    ProductId = detail.ProductId,
                    ProductName = detail.Product?.ProductName,
                    ProductCode = detail.Product?.ProductCode,
                    Description = $"Giảm {detail.AdjustmentType} {adjustment.AdjustmentCode}",
                    PostingDate = adjustment.AccountingDate,
                    CreatedBy = "System"
                });

                // ========== BÚT TOÁN THUẾ GIẢM ==========
                if (detail.TaxAmount > 0)
                {
                    var absTax = Math.Abs(detail.TaxAmount);

                    await CreateEntryAsync(new JournalEntry
                    {
                        VoucherType = "RevenueAdjustment",
                        VoucherId = adjustment.Id,
                        VoucherCode = adjustment.AdjustmentCode,
                        VoucherDate = adjustment.AdjustmentDate,
                        AccountId = detail.TaxAccountId ?? 3333,  // Thuế (bây giờ là nợ)
                        EntryType = "Debit",
                        Amount = absTax,
                        CustomerId = adjustment.CustomerId,
                        ProductId = detail.ProductId,
                        TaxRate = detail.TaxRateSnapshot,
                        Description = $"Thuế giảm {adjustment.AdjustmentCode}",
                        PostingDate = adjustment.AccountingDate,
                        CreatedBy = "System"
                    });

                    await CreateEntryAsync(new JournalEntry
                    {
                        VoucherType = "RevenueAdjustment",
                        VoucherId = adjustment.Id,
                        VoucherCode = adjustment.AdjustmentCode,
                        VoucherDate = adjustment.AdjustmentDate,
                        AccountId = 333,  // Thuế phải nộp (bây giờ là có)
                        EntryType = "Credit",
                        Amount = absTax,
                        CustomerId = adjustment.CustomerId,
                        ProductId = detail.ProductId,
                        TaxRate = detail.TaxRateSnapshot,
                        Description = $"Thuế giảm {adjustment.AdjustmentCode}",
                        PostingDate = adjustment.AccountingDate,
                        CreatedBy = "System"
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Tạo 1 bút toán
        /// </summary>
        private async Task CreateEntryAsync(JournalEntry entry)
        {
            _context.JournalEntries.Add(entry);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Xóa tất cả bút toán của 1 chứng từ
        /// </summary>
        public async Task RemoveEntriesByVoucherAsync(int voucherId, string voucherType)
        {
            var entries = await _context.JournalEntries
                .Where(x => x.VoucherId == voucherId && x.VoucherType == voucherType)
                .ToListAsync();

            if (entries.Count > 0)
            {
                _context.JournalEntries.RemoveRange(entries);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Lấy bút toán của 1 chứng từ
        /// </summary>
        public async Task<List<JournalEntry>> GetEntriesByVoucherAsync(int voucherId, string voucherType)
        {
            return await _context.JournalEntries
                .Include(x => x.Account)
                .Where(x => x.VoucherId == voucherId && x.VoucherType == voucherType)
                .OrderBy(x => x.PostingDate)
                .ThenBy(x => x.AccountId)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy bút toán trong khoảng thời gian
        /// </summary>
        public async Task<List<JournalEntry>> GetEntriesByPeriodAsync(
            DateTime fromDate,
            DateTime toDate,
            int? accountId = null)
        {
            var query = _context.JournalEntries
                .Include(x => x.Account)
                .Where(x => x.PostingDate >= fromDate && x.PostingDate <= toDate);

            if (accountId.HasValue)
                query = query.Where(x => x.AccountId == accountId);

            return await query
                .OrderBy(x => x.PostingDate)
                .ThenBy(x => x.AccountId)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy tổng Nợ/Có của 1 TK trong khoảng thời gian
        /// </summary>
        public async Task<(decimal TotalDebit, decimal TotalCredit)> GetAccountTotalsAsync(
            int accountId,
            DateTime fromDate,
            DateTime toDate)
        {
            var entries = await _context.JournalEntries
                .Where(x => x.AccountId == accountId
                         && x.PostingDate >= fromDate
                         && x.PostingDate <= toDate)
                .ToListAsync();

            var totalDebit = entries.Where(x => x.EntryType == "Debit").Sum(x => x.Amount);
            var totalCredit = entries.Where(x => x.EntryType == "Credit").Sum(x => x.Amount);

            return (totalDebit, totalCredit);
        }
    }
}

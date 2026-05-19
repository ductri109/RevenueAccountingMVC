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
        /// Hàm hỗ trợ: Lấy Khóa chính (Id) của Tài khoản dựa trên Số tài khoản
        /// </summary>
        private async Task<int> GetAccountIdAsync(string accountNumber)
        {
            var acc = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
            if (acc == null)
                throw new Exception($"Hệ thống không tìm thấy số tài khoản '{accountNumber}' trong Danh mục Tài khoản. Vui lòng thêm tài khoản này trước khi hạch toán.");

            return acc.Id;
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

            // 🔥 TÌM ID THỰC SỰ CỦA CÁC TÀI KHOẢN MẶC ĐỊNH 🔥
            int defaultDebitId = await GetAccountIdAsync("131");
            int defaultCreditId = await GetAccountIdAsync("511");
            int defaultTaxParentId = await GetAccountIdAsync("333");

            // 👇 CHỈ CẦN SỬA DÒNG NÀY THÀNH 33311 👇
            int defaultTaxChildId = await GetAccountIdAsync("33311");
            foreach (var detail in voucher.Details)
            {
                // ========== BÚT TOÁN HÀNG BÁN ==========
                await CreateEntryAsync(new JournalEntry
                {
                    VoucherType = "SalesVoucher",
                    VoucherId = voucher.Id,
                    VoucherCode = voucher.VoucherCode,
                    VoucherDate = voucher.AccountingDate,
                    AccountId = detail.DebitAccountId ?? defaultDebitId, // Dùng ID tra cứu được
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
                    AccountId = detail.CreditAccountId ?? defaultCreditId, // Dùng ID tra cứu được
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
                if (detail.TaxAmount > 0)
                {
                    await CreateEntryAsync(new JournalEntry
                    {
                        VoucherType = "SalesVoucher",
                        VoucherId = voucher.Id,
                        VoucherCode = voucher.VoucherCode,
                        VoucherDate = voucher.AccountingDate,
                        AccountId = defaultTaxParentId,  // ĐÃ SỬA: Không hardcode số 333 nữa
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
                        AccountId = detail.TaxAccountId ?? defaultTaxChildId, // ĐÃ SỬA: Không hardcode số 3333 nữa
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

            var oldEntries = await _context.JournalEntries
                .Where(x => x.VoucherId == adjustmentId && x.VoucherType == "RevenueAdjustment")
                .ToListAsync();
            if (oldEntries.Count > 0)
                _context.JournalEntries.RemoveRange(oldEntries);

            // 🔥 TÌM ID THỰC SỰ CỦA CÁC TÀI KHOẢN MẶC ĐỊNH 🔥
            int defaultTaxParentId = await GetAccountIdAsync("333");
            int defaultTaxChildId = await GetAccountIdAsync("33311");

            foreach (var detail in adjustment.Details)
            {
                var absAmount = Math.Abs(detail.Amount);

                // 🔥 FIX: EntryType phải khớp với vị trí chọn của tài khoản, không đảo!
                await CreateEntryAsync(new JournalEntry
                {
                    VoucherType = "RevenueAdjustment",
                    VoucherId = adjustment.Id,
                    VoucherCode = adjustment.AdjustmentCode,
                    VoucherDate = adjustment.AdjustmentDate,
                    AccountId = detail.DebitAccountId.Value,
                    EntryType = "Debit",  // TK chọn ở vị trí Debit → EntryType = Debit
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
                    AccountId = detail.CreditAccountId.Value,
                    EntryType = "Credit",  // TK chọn ở vị trí Credit → EntryType = Credit
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

                if (detail.TaxAmount < 0 || detail.TaxAmount > 0)
                {
                    var absTax = Math.Abs(detail.TaxAmount);

                    await CreateEntryAsync(new JournalEntry
                    {
                        VoucherType = "RevenueAdjustment",
                        VoucherId = adjustment.Id,
                        VoucherCode = adjustment.AdjustmentCode,
                        VoucherDate = adjustment.AdjustmentDate,
                        AccountId = detail.TaxAccountId ?? defaultTaxChildId,
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
                        AccountId = defaultTaxParentId,
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

        private async Task CreateEntryAsync(JournalEntry entry)
        {
            _context.JournalEntries.Add(entry);
            await _context.SaveChangesAsync();
        }

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

        public async Task<List<JournalEntry>> GetEntriesByVoucherAsync(int voucherId, string voucherType)
        {
            return await _context.JournalEntries
                .Include(x => x.Account)
                .Where(x => x.VoucherId == voucherId && x.VoucherType == voucherType)
                .OrderBy(x => x.PostingDate)
                .ThenBy(x => x.AccountId)
                .ToListAsync();
        }

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
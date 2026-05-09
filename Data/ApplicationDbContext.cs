using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RevenueAccountingMVC.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Tax> Taxes { get; set; }
        public DbSet<SalesVoucher> SalesVouchers { get; set; }
        public DbSet<SalesVoucherDetail> SalesVoucherDetails { get; set; }
        public DbSet<RevenueAdjustment> RevenueAdjustments { get; set; }
        public DbSet<RevenueAdjustmentDetail> RevenueAdjustmentDetails { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<RevenueReportLine> RevenueReportLines { get; set; }
        public DbSet<RevenueByProductReportLine> RevenueByProductReportLines { get; set; }
        // THÊM ĐOẠN NÀY ĐỂ FIX LỖI MULTIPLE CASCADE PATHS
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<RevenueAdjustment>()
                .HasOne(r => r.OriginalSalesVoucher)
                .WithMany()
                .HasForeignKey(r => r.OriginalSalesVoucherId)
                .OnDelete(DeleteBehavior.Restrict); // Tắt Cascade Delete

            modelBuilder.Entity<RevenueAdjustment>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); // Tắt Cascade Delete
        }

        // ========== HÀM SEED DỮ LIỆU ==========
        public async Task SeedSalesAndRevenueAdjustmentDataAsync()
        {
            // Kiểm tra xem đã có dữ liệu chưa
            if (SalesVouchers.Any())
                return;

            try
            {
                // Lấy dữ liệu cơ bản
                var customers = await Customers.Where(c => c.Status == CustomerStatus.Active).ToListAsync();
                var products = await Products.Where(p => p.Status == ProductStatus.Active).ToListAsync();
                var accounts = await Accounts.ToListAsync();
                var taxes = await Taxes.Where(t => t.Status == TaxStatus.Active).ToListAsync();

                if (!customers.Any() || !products.Any() || !accounts.Any())
                    return; // Không có dữ liệu cơ bản

                // Lấy tài khoản chính
                var cashAccount = accounts.FirstOrDefault(a => a.AccountNumber == "111"); // Tiền mặt
                var receivableAccount = accounts.FirstOrDefault(a => a.AccountNumber == "131"); // Phải thu của khách
                var receivableDetailAccount = accounts.FirstOrDefault(a => a.AccountNumber == "1311"); // Phải thu chi tiết
                var taxAccount = accounts.FirstOrDefault(a => a.AccountNumber == "33311"); // Thuế GTGT phải nộp
                var revenueAccount = accounts.FirstOrDefault(a => a.AccountNumber == "511"); // Doanh thu bán hàng
                var reductionAccount = accounts.FirstOrDefault(a => a.AccountNumber == "521"); // Giảm trừ doanh thu

                if (cashAccount == null || receivableAccount == null || taxAccount == null || revenueAccount == null)
                    return;

                // Tính ngày động: từ năm ngoái đến hôm nay
                var today = DateTime.Now.Date;
                var oneYearAgo = today.AddYears(-1);
                var dateRange = (int)(today - oneYearAgo).TotalDays;

                var random = new Random(42); // Seed cố định để dữ liệu không đổi

                // ====== TẠO 30 SALSVOUCHER ======
                for (int i = 1; i <= 30; i++)
                {
                    var customer = customers[random.Next(customers.Count)];
                    var product = products[random.Next(products.Count)];
                    var tax = taxes.FirstOrDefault() ?? null;

                    var voucherDate = oneYearAgo.AddDays(random.Next(dateRange));
                    var quantity = random.Next(1, 10);
                    var unitPrice = (decimal)(random.Next(50, 500) * 1000); // 50k - 500k
                    var amount = quantity * unitPrice;
                    var taxRate = tax?.TaxRate ?? 10m;
                    var taxAmount = amount * taxRate / 100;
                    var totalPayment = amount + taxAmount;

                    var salesVoucher = new SalesVoucher
                    {
                        VoucherCode = SalesVoucher.GenerateVoucherCode(i),
                        InvoiceNumber = $"INV{voucherDate.Year}{i:D3}",
                        AccountingDate = voucherDate,
                        CustomerId = customer.Id,
                        CustomerNameSnapshot = customer.CustomerName,
                        CustomerAddressSnapshot = customer.Address,
                        Description = $"Bán {product.ProductName} cho {customer.CustomerName}",
                        DebtDays = customer.MaxDebtDays ?? 30,
                        DueDate = voucherDate.AddDays(customer.MaxDebtDays ?? 30),
                        TotalAmount = amount,
                        TotalTaxAmount = taxAmount,
                        TotalPayment = totalPayment,
                        Status = VoucherStatus.Posted,
                        CreatedAt = voucherDate,
                        Details = new List<SalesVoucherDetail>
                        {
                            new SalesVoucherDetail
                            {
                                ProductId = product.Id,
                                ProductNameSnapshot = product.ProductName,
                                UnitSnapshot = product.Unit,
                                DebitAccountId = receivableDetailAccount?.Id ?? receivableAccount.Id,
                                CreditAccountId = revenueAccount.Id,
                                Quantity = quantity,
                                UnitPrice = unitPrice,
                                DiscountRate = 0,
                                Amount = amount,
                                TaxId = tax?.Id,
                                TaxRateSnapshot = taxRate,
                                TaxAccountId = taxAccount.Id,
                                TaxAmount = taxAmount
                            }
                        }
                    };

                    SalesVouchers.Add(salesVoucher);

                    // ====== BÚT TOÁN CHO SALSVOUCHER ======
                    // Nợ TK 131/1311: Phải thu của khách hàng
                    JournalEntries.Add(new JournalEntry
                    {
                        VoucherType = "SalesVoucher",
                        VoucherId = salesVoucher.Id,
                        VoucherCode = salesVoucher.VoucherCode,
                        VoucherDate = voucherDate,
                        AccountId = receivableDetailAccount?.Id ?? receivableAccount.Id,
                        EntryType = "Debit",
                        Amount = totalPayment,
                        CustomerId = customer.Id,
                        CustomerName = customer.CustomerName,
                        CustomerCode = customer.CustomerCode,
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        ProductCode = product.ProductCode,
                        TaxId = tax?.Id,
                        TaxRate = taxRate,
                        Description = $"Ghi nhận doanh thu bán {product.ProductName}"
                    });

                    // Có TK 511: Doanh thu bán hàng
                    JournalEntries.Add(new JournalEntry
                    {
                        VoucherType = "SalesVoucher",
                        VoucherId = salesVoucher.Id,
                        VoucherCode = salesVoucher.VoucherCode,
                        VoucherDate = voucherDate,
                        AccountId = revenueAccount.Id,
                        EntryType = "Credit",
                        Amount = amount,
                        CustomerId = customer.Id,
                        CustomerName = customer.CustomerName,
                        CustomerCode = customer.CustomerCode,
                        ProductId = product.Id,
                        ProductName = product.ProductName,
                        ProductCode = product.ProductCode,
                        TaxId = tax?.Id,
                        TaxRate = taxRate,
                        Description = $"Ghi nhận doanh thu bán {product.ProductName}"
                    });

                    // Nợ TK 33311: Thuế GTGT phải nộp
                    if (taxAmount > 0)
                    {
                        JournalEntries.Add(new JournalEntry
                        {
                            VoucherType = "SalesVoucher",
                            VoucherId = salesVoucher.Id,
                            VoucherCode = salesVoucher.VoucherCode,
                            VoucherDate = voucherDate,
                            AccountId = taxAccount.Id,
                            EntryType = "Debit",
                            Amount = taxAmount,
                            CustomerId = customer.Id,
                            CustomerName = customer.CustomerName,
                            CustomerCode = customer.CustomerCode,
                            ProductId = product.Id,
                            ProductName = product.ProductName,
                            ProductCode = product.ProductCode,
                            TaxId = tax?.Id,
                            TaxRate = taxRate,
                            Description = $"Ghi nhận thuế GTGT {taxRate}% - {product.ProductName}"
                        });

                        // Có TK 111: Tiền mặt (Hoặc TK Ngân hàng)
                        JournalEntries.Add(new JournalEntry
                        {
                            VoucherType = "SalesVoucher",
                            VoucherId = salesVoucher.Id,
                            VoucherCode = salesVoucher.VoucherCode,
                            VoucherDate = voucherDate,
                            AccountId = cashAccount.Id,
                            EntryType = "Credit",
                            Amount = taxAmount,
                            CustomerId = customer.Id,
                            CustomerName = customer.CustomerName,
                            CustomerCode = customer.CustomerCode,
                            ProductId = product.Id,
                            ProductName = product.ProductName,
                            ProductCode = product.ProductCode,
                            TaxId = tax?.Id,
                            TaxRate = taxRate,
                            Description = $"Ghi nhận nợ thuế GTGT {taxRate}%"
                        });
                    }
                }

                await SaveChangesAsync();

                // ====== TẠO 30 REVENUEADJUSTMENT (GIẢM GIÁ) ======
                var salesVouchers = await SalesVouchers.Include(s => s.Details).ToListAsync();
                var journalCounter = (await JournalEntries.CountAsync()) + 1;

                for (int i = 1; i <= 30; i++)
                {
                    var originalVoucher = salesVouchers[random.Next(salesVouchers.Count)];
                    var adjustmentDate = originalVoucher.AccountingDate.AddDays(random.Next(1, 60)); // Giảm sau 1-60 ngày
                    var adjustmentRate = (decimal)random.Next(5, 25) / 100; // Giảm 5%-25%

                    var discountAmount = originalVoucher.TotalAmount * adjustmentRate;
                    var discountTax = originalVoucher.TotalTaxAmount * adjustmentRate;
                    var discountPayment = discountAmount + discountTax;

                    var adjustment = new RevenueAdjustment
                    {
                        AdjustmentCode = RevenueAdjustment.GenerateAdjustmentCode(i),
                        AdjustmentDate = adjustmentDate,
                        AccountingDate = adjustmentDate,
                        CustomerId = originalVoucher.CustomerId,
                        OriginalSalesVoucherId = originalVoucher.Id,
                        Description = $"Giảm giá cho đơn {originalVoucher.VoucherCode} ({adjustmentRate:P})",
                        TotalDiscountAmount = -discountAmount, // Âm
                        TotalTaxAmount = -discountTax, // Âm
                        TotalPayment = -discountPayment, // Âm
                        Status = VoucherStatus.Posted,
                        CreatedAt = adjustmentDate,
                        Details = new List<RevenueAdjustmentDetail>()
                    };

                    // Thêm chi tiết điều chỉnh từ chứng từ gốc
                    foreach (var detail in originalVoucher.Details)
                    {
                        var adjDetail = new RevenueAdjustmentDetail
                        {
                            ProductId = detail.ProductId,
                            AdjustmentType = "GiamGia",
                            DebitAccountId = revenueAccount.Id,
                            CreditAccountId = detail.DebitAccountId,
                            Quantity = detail.Quantity,
                            UnitPrice = detail.UnitPrice,
                            DiscountRate = 0,
                            Amount = -(detail.Amount * adjustmentRate), // Âm
                            TaxId = detail.TaxId,
                            TaxRateSnapshot = detail.TaxRateSnapshot,
                            TaxAccountId = detail.TaxAccountId,
                            TaxAmount = -(detail.TaxAmount * adjustmentRate) // Âm
                        };
                        adjustment.Details.Add(adjDetail);
                    }

                    RevenueAdjustments.Add(adjustment);

                    // ====== BÚT TOÁN CHO REVENUEADJUSTMENT ======
                    // Nợ TK 511: Giảm doanh thu
                    JournalEntries.Add(new JournalEntry
                    {
                        VoucherType = "RevenueAdjustment",
                        VoucherId = adjustment.Id,
                        VoucherCode = adjustment.AdjustmentCode,
                        VoucherDate = adjustmentDate,
                        AccountId = revenueAccount.Id,
                        EntryType = "Debit",
                        Amount = discountAmount,
                        CustomerId = originalVoucher.CustomerId,
                        CustomerName = originalVoucher.CustomerNameSnapshot,
                        CustomerCode = originalVoucher.Customer?.CustomerCode,
                        Description = $"Giảm doanh thu {adjustmentRate:P} cho đơn {originalVoucher.VoucherCode}"
                    });

                    // Có TK 131/1311: Hoàn tiền hàng
                    JournalEntries.Add(new JournalEntry
                    {
                        VoucherType = "RevenueAdjustment",
                        VoucherId = adjustment.Id,
                        VoucherCode = adjustment.AdjustmentCode,
                        VoucherDate = adjustmentDate,
                        AccountId = receivableDetailAccount?.Id ?? receivableAccount.Id,
                        EntryType = "Credit",
                        Amount = discountAmount,
                        CustomerId = originalVoucher.CustomerId,
                        CustomerName = originalVoucher.CustomerNameSnapshot,
                        CustomerCode = originalVoucher.Customer?.CustomerCode,
                        Description = $"Giảm phải thu khách {adjustmentRate:P}"
                    });

                    // Nợ TK 111: Hoàn tiền khách
                    if (discountTax > 0)
                    {
                        JournalEntries.Add(new JournalEntry
                        {
                            VoucherType = "RevenueAdjustment",
                            VoucherId = adjustment.Id,
                            VoucherCode = adjustment.AdjustmentCode,
                            VoucherDate = adjustmentDate,
                            AccountId = cashAccount.Id,
                            EntryType = "Debit",
                            Amount = discountTax,
                            CustomerId = originalVoucher.CustomerId,
                            CustomerName = originalVoucher.CustomerNameSnapshot,
                            CustomerCode = originalVoucher.Customer?.CustomerCode,
                            Description = $"Hoàn lại tiền mặt - giảm thuế"
                        });

                        // Có TK 33311: Giảm thuế
                        JournalEntries.Add(new JournalEntry
                        {
                            VoucherType = "RevenueAdjustment",
                            VoucherId = adjustment.Id,
                            VoucherCode = adjustment.AdjustmentCode,
                            VoucherDate = adjustmentDate,
                            AccountId = taxAccount.Id,
                            EntryType = "Credit",
                            Amount = discountTax,
                            CustomerId = originalVoucher.CustomerId,
                            CustomerName = originalVoucher.CustomerNameSnapshot,
                            CustomerCode = originalVoucher.Customer?.CustomerCode,
                            Description = $"Giảm thuế GTGT phải nộp {adjustmentRate:P}"
                        });
                    }
                }

                await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi seed data: {ex.Message}");
            }
        }
    }
}
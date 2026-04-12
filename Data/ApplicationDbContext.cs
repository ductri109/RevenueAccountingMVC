using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Models;

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
    }
}
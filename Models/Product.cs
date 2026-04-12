using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public enum ProductStatus
    {
        [Display(Name = "Không hoạt động")]
        Inactive = 1,
        [Display(Name = "Hoạt động")]
        Active = 2
    }

    public enum ProductType
    {
        [Display(Name = "Hàng hóa")]
        Goods = 1,
        [Display(Name = "Dịch vụ")]
        Service = 2
    }

    public class Product
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(20)]
        public string? ProductCode { get; set; } // ⚠️ UNIQUE

        [Required]
        public string ProductName { get; set; }

        public ProductType ProductType { get; set; } // ✅ FIX

        [Required]
        public string Unit { get; set; }

        public ProductStatus Status { get; set; }

        // 🔥 FIX: FK
        public int? RevenueAccountId { get; set; }

        [ForeignKey("RevenueAccountId")]
        public Account? RevenueAccount { get; set; }

        public int? InventoryAccountId { get; set; }

        [ForeignKey("InventoryAccountId")]
        public Account? InventoryAccount { get; set; }

        public int? TaxId { get; set; }

        [ForeignKey("TaxId")]
        public Tax? Tax { get; set; }

        public decimal? DefaultUnitPrice { get; set; }

        public decimal? DefaultTaxRate { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // ✅ IMPROVED
        public static string GenerateProductCode(int nextNumber)
        {
            return $"SP{nextNumber:D3}";
        }
    }
}
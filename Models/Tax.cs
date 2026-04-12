using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public enum TaxStatus
    {
        Active = 1,
        Inactive = 2
    }

    public class Tax
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(20)]
        public string? TaxCode { get; set; } // ⚠️ UNIQUE

        [Required]
        public string TaxName { get; set; }

        public string? SecondaryName { get; set; }

        public decimal TaxRate { get; set; }

        public bool IsDeductible { get; set; }

        // 🔥 FIX: FK
        public int TaxAccountId { get; set; }

        [ValidateNever] // <--- THÊM DÒNG NÀY
        [ForeignKey("TaxAccountId")]
        public Account? TaxAccount { get; set; } // <--- THÊM DẤU ?

        public TaxStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // ✅ IMPROVED
        public static string GenerateTaxCode(int nextNumber)
        {
            return $"R{nextNumber:D3}";
        }
    }
}
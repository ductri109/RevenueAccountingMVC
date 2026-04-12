using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public enum CustomerStatus
    {
        Active = 1,
        Inactive = 2
    }

    public enum CustomerType
    {
        Personal = 1,
        Business = 2
    }

    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(20)]
        public string? CustomerCode { get; set; } // ⚠️ UNIQUE

        [Required]
        public string CustomerName { get; set; }

        public CustomerType CustomerType { get; set; }

        public string? Address { get; set; }

        [Required]
        public string PhoneNumber { get; set; }

        public string? Email { get; set; }

        public string? TaxCode { get; set; }

        // 🔥 FIX: string → FK
        public int? ReceivableAccountId { get; set; }

        [ForeignKey("ReceivableAccountId")]
        public Account? ReceivableAccount { get; set; }

        public int? MaxDebtDays { get; set; }

        public string? ContactPerson { get; set; }

        public CustomerStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // ✅ IMPROVED: generate code
        public static string GenerateCustomerCode(int nextNumber)
        {
            return $"KH{nextNumber:D4}";
        }
    }
}
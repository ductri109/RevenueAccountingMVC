using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public enum AccountCategory
    {
        Asset = 1,
        LiabilityAndEquity = 2,
        Revenue = 3,
        Expense = 4
    }

    public enum AccountNature
    {
        Debit = 1,
        Credit = 2,
        Both = 3,
        None = 4
    }

    public enum AccountStatus
    {
        Active = 1,
        Inactive = 2
    }

    public class Account
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string AccountNumber { get; set; } // ⚠️ UNIQUE (thêm index DB)

        [Required]
        [MaxLength(255)]
        public string AccountName { get; set; }

        // ❌ ĐÃ XÓA Level

        public AccountCategory Category { get; set; }

        public int? ParentAccountId { get; set; }

        [ForeignKey("ParentAccountId")]
        public Account? ParentAccount { get; set; }

        public ICollection<Account>? Children { get; set; }

        public AccountNature Nature { get; set; }

        public bool IsDetail { get; set; }

        public string? Description { get; set; }

        public AccountStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
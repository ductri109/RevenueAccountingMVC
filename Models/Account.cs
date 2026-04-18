using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public enum AccountCategory
    {
        [Display(Name = "Tài sản")]
        Asset = 1,

        [Display(Name = "Nợ phải trả & Vốn chủ sở hữu")]
        LiabilityAndEquity = 2,

        [Display(Name = "Doanh thu")]
        Revenue = 3,

        [Display(Name = "Chi phí")]
        Expense = 4
    }

    public enum AccountNature
    {
        [Display(Name = "Dư Nợ")]
        Debit = 1,

        [Display(Name = "Dư Có")]
        Credit = 2,

        [Display(Name = "Lưỡng tính")]
        Both = 3,

        [Display(Name = "Không có số dư")]
        None = 4
    }

    public enum AccountStatus
    {
        [Display(Name = "Hoạt động")]
        Active = 1,

        [Display(Name = "Ngừng sử dụng")]
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
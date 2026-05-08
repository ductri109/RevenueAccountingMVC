using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    /// <summary>
    /// Báo cáo doanh thu theo khách hàng
    /// </summary>
    public class RevenueByCustomerReportLine
    {
        [Key]
        public int Id { get; set; }

        // ========== KHOẢNG THỜI GIAN ==========
        [Required]
        public DateTime PeriodStart { get; set; }
        [Required]
        public DateTime PeriodEnd { get; set; }

        // ========== KHÁCH HÀNG ==========
        [Required]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [MaxLength(20)]
        public string? CustomerCode { get; set; }

        [Required]
        [MaxLength(255)]
        public string CustomerName { get; set; }

        [MaxLength(50)]
        public string? TaxCode { get; set; }

        // ========== DOANH THU ==========
        [Display(Name = "Số giao dịch")]
        public int TransactionCount { get; set; }

        [Display(Name = "Doanh thu")]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Thuế")]
        public decimal TotalTax { get; set; }

        [Display(Name = "Tổng thanh toán")]
        public decimal TotalPayment { get; set; }

        // ========== DOANH THU GIẢM ==========
        [Display(Name = "Doanh thu giảm")]
        public decimal AdjustmentRevenue { get; set; }  // Âm

        [Display(Name = "Doanh thu ròng")]
        public decimal NetRevenue { get { return TotalRevenue + AdjustmentRevenue; } }

        // ========== THỐNG KÊ ==========
        [Display(Name = "Giá trị TB")]
        public decimal AverageOrderValue { get; set; }

        [Display(Name = "Số ngày nợ")]
        public int AverageDebtDays { get; set; }

        // ========== AUDIT ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string? GeneratedBy { get; set; }
    }
}

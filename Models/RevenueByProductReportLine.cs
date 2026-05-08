using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    /// <summary>
    /// Báo cáo doanh thu theo sản phẩm
    /// </summary>
    public class RevenueByProductReportLine
    {
        [Key]
        public int Id { get; set; }

        // ========== KHOẢNG THỜI GIAN ==========
        [Required]
        public DateTime PeriodStart { get; set; }
        [Required]
        public DateTime PeriodEnd { get; set; }

        // ========== SẢN PHẨM ==========
        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [MaxLength(20)]
        public string? ProductCode { get; set; }

        [Required]
        [MaxLength(255)]
        public string ProductName { get; set; }

        [MaxLength(50)]
        public string? Unit { get; set; }

        // ========== DOANH THU BÁN ==========
        [Display(Name = "SL bán")]
        public decimal Quantity { get; set; }

        [Display(Name = "Giá TB")]
        public decimal AverageUnitPrice { get; set; }

        [Display(Name = "Doanh thu")]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Thuế")]
        public decimal TotalTax { get; set; }

        [Display(Name = "Tổng thanh toán")]
        public decimal TotalPayment { get; set; }

        // ========== DOANH THU GIẢM ==========
        [Display(Name = "SL giảm")]
        public decimal AdjustmentQuantity { get; set; }

        [Display(Name = "Doanh thu giảm")]
        public decimal AdjustmentRevenue { get; set; }  // Âm

        // ========== DOANH THU RÒNG ==========
        [Display(Name = "SL ròng")]
        public decimal NetQuantity { get { return Quantity + AdjustmentQuantity; } }

        [Display(Name = "Doanh thu ròng")]
        public decimal NetRevenue { get { return TotalRevenue + AdjustmentRevenue; } }

        // ========== THỐNG KÊ ==========
        [Display(Name = "Số KH")]
        public int CustomerCount { get; set; }

        // ========== AUDIT ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string? GeneratedBy { get; set; }
    }
}

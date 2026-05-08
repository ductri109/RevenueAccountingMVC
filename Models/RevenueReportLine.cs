using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    /// <summary>
    /// Báo cáo doanh thu theo sản phẩm tính từ ngày A đến ngày B
    /// Có thể tạo từ JournalEntry hoặc từ SalesVoucher + RevenueAdjustment
    /// </summary>
    public class RevenueReportLine
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
        public string? ProductCode { get; set; }  // Snapshot

        [Required]
        [MaxLength(255)]
        public string ProductName { get; set; }

        [MaxLength(50)]
        public string? Unit { get; set; }  // Snapshot

        // ========== DOANH THU BÁN ==========
        [Display(Name = "SL bán")]
        public decimal Quantity { get; set; }

        [Display(Name = "Giá bình quân")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Doanh thu")]
        public decimal TotalRevenue { get; set; }

        [Display(Name = "Thuế GTGT")]
        public decimal TotalTax { get; set; }

        [Display(Name = "Tổng thanh toán")]
        public decimal TotalPayment { get; set; }   // = TotalRevenue + TotalTax

        // ========== DOANH THU GIẢM (từ RevenueAdjustment) ==========
        [Display(Name = "SL giảm")]
        public decimal AdjustmentQuantity { get; set; }

        [Display(Name = "Doanh thu giảm")]
        public decimal AdjustmentRevenue { get; set; }  // Luôn âm

        [Display(Name = "Thuế giảm")]
        public decimal AdjustmentTax { get; set; }      // Luôn âm

        // ========== DOANH THU RÒNG ==========
        [Display(Name = "SL ròng")]
        public decimal NetQuantity { get { return Quantity + AdjustmentQuantity; } }

        [Display(Name = "Doanh thu ròng")]
        public decimal NetRevenue { get { return TotalRevenue + AdjustmentRevenue; } }

        // ========== THÔNG TIN BỔ SUNG ==========
        [Display(Name = "Số KH mua")]
        public int CustomerCount { get; set; }

        // ========== AUDIT ==========
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string? GeneratedBy { get; set; }
    }
}

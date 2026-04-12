using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RevenueAccountingMVC.ViewModels
{
    public class RevenueAdjustmentViewModel
    {
        public string? AdjustmentCode { get; set; } 
        
        [Required(ErrorMessage = "Vui lòng chọn ngày chứng từ")]
        public DateTime AdjustmentDate { get; set; } = DateTime.Now.Date;
        
        [Required(ErrorMessage = "Vui lòng chọn ngày hạch toán")]
        public DateTime AccountingDate { get; set; } = DateTime.Now.Date;

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? TaxCode { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chứng từ bán hàng")]
        public int OriginalSalesVoucherId { get; set; }
        
        // Thông tin hiển thị của CT gốc
        public DateTime? OriginalVoucherDate { get; set; }
        public decimal OriginalTotalAmount { get; set; }
        public decimal OriginalTotalTax { get; set; }
        public decimal OriginalTotalPayment { get; set; }

        public List<RevenueAdjustmentDetailVM> Details { get; set; } = new List<RevenueAdjustmentDetailVM>();

        public decimal TotalDiscountAmount { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal TotalPayment { get; set; }
    }

    public class RevenueAdjustmentDetailVM
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal Amount { get; set; }
        
        [Required]
        public string AdjustmentType { get; set; } // GiamGia, TraLai, ChietKhau
        
        // Dữ liệu ẩn để JS Validate không vượt quá số lượng/đơn giá gốc
        public decimal OriginalQty { get; set; } 
        public decimal OriginalPrice { get; set; }
        public decimal OriginalTaxRate { get; set; }
    }
}
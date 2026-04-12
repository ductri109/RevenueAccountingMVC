using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public class RevenueAdjustment
    {
        [Key]
        public int Id { get; set; }

        [ValidateNever]
        [Required]
        [MaxLength(20)]
        [Display(Name = "Số chứng từ")]
        public string AdjustmentCode { get; set; } // CTGG001

        [Required]
        [Display(Name = "Ngày chứng từ")]
        public DateTime AdjustmentDate { get; set; } = DateTime.Now.Date;

        [Required]
        [Display(Name = "Ngày hạch toán")]
        public DateTime AccountingDate { get; set; } = DateTime.Now.Date;

        [Required]
        [Display(Name = "Khách hàng")]
        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Display(Name = "Diễn giải")]
        public string? Description { get; set; }

        // Liên kết chứng từ gốc
        [Required]
        [Display(Name = "Chứng từ bán hàng gốc")]
        public int OriginalSalesVoucherId { get; set; }
        [ForeignKey("OriginalSalesVoucherId")]
        public SalesVoucher? OriginalSalesVoucher { get; set; }

        [Display(Name = "Tổng tiền giảm")]
        public decimal TotalDiscountAmount { get; set; } = 0; // Luôn âm

        [Display(Name = "Tổng thuế giảm")]
        public decimal TotalTaxAmount { get; set; } = 0; // Luôn âm

        [Display(Name = "Tổng thanh toán")]
        public decimal TotalPayment { get; set; } = 0; // Luôn âm

        public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }

        public virtual List<RevenueAdjustmentDetail> Details { get; set; } = new List<RevenueAdjustmentDetail>();

        public static string GenerateAdjustmentCode(int nextNumber)
        {
            return $"CTGG{nextNumber:D4}";
        }
    }
}
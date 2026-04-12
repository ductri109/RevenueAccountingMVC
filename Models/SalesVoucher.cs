using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public enum VoucherStatus
    {
        [Display(Name = "Chưa ghi sổ")] Draft = 1,
        [Display(Name = "Đã ghi sổ")] Posted = 2
    }

    public class SalesVoucher
    {
        [Key]
        public int Id { get; set; }

        [ValidateNever]
        [Required]
        [MaxLength(20)]
        [Display(Name = "Số chứng từ")]
        public string VoucherCode { get; set; } // Tự sinh: CT0001...

        [Required]
        [Display(Name = "Ngày hạch toán")]
        public DateTime AccountingDate { get; set; } = DateTime.Now.Date;

        [Required]
        [Display(Name = "Khách hàng")]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        // Lưu snapshot để lỡ sau này tên/địa chỉ khách đổi thì hóa đơn cũ không bị đổi
        public string? CustomerNameSnapshot { get; set; }
        public string? CustomerAddressSnapshot { get; set; }

        [Display(Name = "Diễn giải")]
        public string? Description { get; set; }

        [Display(Name = "Số ngày nợ")]
        public int DebtDays { get; set; } = 0;

        [Display(Name = "Ngày đáo hạn")]
        public DateTime DueDate { get; set; }

        [Display(Name = "Tổng tiền hàng")]
        public decimal TotalAmount { get; set; } = 0;

        [Display(Name = "Tổng tiền thuế")]
        public decimal TotalTaxAmount { get; set; } = 0;

        [Display(Name = "Tổng thanh toán")]
        public decimal TotalPayment { get; set; } = 0;

        [Display(Name = "Trạng thái")]
        public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; } // Bỏ qua nếu chưa làm Auth

        // Danh sách hàng hóa chi tiết
        public virtual List<SalesVoucherDetail> Details { get; set; } = new List<SalesVoucherDetail>();

        // Hàm sinh mã chứng từ
        public static string GenerateVoucherCode(int nextNumber)
        {
            return $"CT{nextNumber:D4}";
        }
    }
}
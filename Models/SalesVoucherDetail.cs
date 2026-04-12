using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public class SalesVoucherDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SalesVoucherId { get; set; }

        [ForeignKey("SalesVoucherId")]
        public SalesVoucher? SalesVoucher { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        public string? ProductNameSnapshot { get; set; }
        public string? UnitSnapshot { get; set; }

        // Tài khoản kế toán
        public int? DebitAccountId { get; set; } // TK Nợ (Thường là 131)
        [ForeignKey("DebitAccountId")] public Account? DebitAccount { get; set; }

        public int? CreditAccountId { get; set; } // TK Có (Thường là 511)
        [ForeignKey("CreditAccountId")] public Account? CreditAccount { get; set; }

        [Required]
        public decimal Quantity { get; set; } = 1;

        [Required]
        public decimal UnitPrice { get; set; } = 0;

        public decimal DiscountRate { get; set; } = 0; // % Chiết khấu

        [Display(Name = "Thành tiền")]
        public decimal Amount { get; set; } = 0; // = Qty * Price * (1 - Discount/100)

        // Thuế
        public int? TaxId { get; set; }
        [ForeignKey("TaxId")] public Tax? Tax { get; set; }

        public decimal TaxRateSnapshot { get; set; } = 0; // % Thuế tại thời điểm bán

        public int? TaxAccountId { get; set; } // TK Thuế (Thường là 33311)
        [ForeignKey("TaxAccountId")] public Account? TaxAccount { get; set; }

        public decimal TaxAmount { get; set; } = 0; // = Amount * TaxRate/100
    }
}
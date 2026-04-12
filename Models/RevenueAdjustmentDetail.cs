using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    public class RevenueAdjustmentDetail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RevenueAdjustmentId { get; set; }
        [ForeignKey("RevenueAdjustmentId")]
        public RevenueAdjustment? RevenueAdjustment { get; set; }

        [Required]
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required]
        [MaxLength(50)]
        public string AdjustmentType { get; set; } // "GiamGia", "TraLai", "ChietKhau"

        public int? DebitAccountId { get; set; } 
        [ForeignKey("DebitAccountId")] public Account? DebitAccount { get; set; }

        public int? CreditAccountId { get; set; } 
        [ForeignKey("CreditAccountId")] public Account? CreditAccount { get; set; }

        [Required]
        public decimal Quantity { get; set; } = 0;

        [Required]
        public decimal UnitPrice { get; set; } = 0;

        public decimal DiscountRate { get; set; } = 0;

        [Display(Name = "Thành tiền")]
        public decimal Amount { get; set; } = 0; // Bắt buộc < 0

        public int? TaxId { get; set; }
        [ForeignKey("TaxId")] public Tax? Tax { get; set; }

        public decimal TaxRateSnapshot { get; set; } = 0;

        public int? TaxAccountId { get; set; }
        [ForeignKey("TaxAccountId")] public Account? TaxAccount { get; set; }

        public decimal TaxAmount { get; set; } = 0;
    }
}
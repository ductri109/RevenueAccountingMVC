using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RevenueAccountingMVC.Models
{
    /// <summary>
    /// Bút toán chi tiết (JournalEntry)
    /// Lưu từng bút toán Nợ/Có sinh ra từ chứng từ
    /// </summary>
    public class JournalEntry
    {
        [Key]
        public int Id { get; set; }

        // ========== THAM CHIẾU CHỨNG từ GỐC ==========
        [Required]
        [MaxLength(20)]
        public string VoucherType { get; set; }  // "SalesVoucher" hoặc "RevenueAdjustment"

        [Required]
        public int VoucherId { get; set; }       // ID chứng từ gốc

        [Required]
        [MaxLength(20)]
        public string VoucherCode { get; set; }  // CT0001, CTGG0001 (lưu snapshot để ko bị đổi)

        [Required]
        public DateTime VoucherDate { get; set; } // Ngày chứng từ

        // ========== TÀI KHOẢN ==========
        [Required]
        public int AccountId { get; set; }

        [ForeignKey("AccountId")]
        public Account? Account { get; set; }

        // ========== CHI TIẾT BÚT TOÁN ==========
        [Required]
        [MaxLength(10)]
        public string EntryType { get; set; }    // "Debit" hoặc "Credit"

        [Required]
        public decimal Amount { get; set; }      // Luôn dương, EntryType quy định Nợ hay Có

        // ========== THÔNG TIN SNAPSHOT ==========
        // Lưu snapshot để dù sau này customer/product thay đổi, báo cáo lịch sử vẫn chính xác

        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }

        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductCode { get; set; }

        public int? TaxId { get; set; }
        public decimal? TaxRate { get; set; }    // % Thuế tại thời điểm phát sinh

        // ========== DIỄN GIẢI ==========
        public string? Description { get; set; }  // VD: "Bán hàng CT0001", "Giảm giá CTGG0001"

        // ========== NGÀY HẠC TOÁN ==========
        [Required]
        public DateTime PostingDate { get; set; } // Ngày ghi sổ (thường = VoucherDate, nhưng có thể khác)

        // ========== AUDIT ==========
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }

        // ========== OPTIONAL: REVERSE ENTRY ==========
        // Để theo dõi bút toán đảo ngược (nếu có)
        public int? ReversalEntryId { get; set; } // Nếu bút toán này là bút toán đảo ngược của entry khác
    }
}

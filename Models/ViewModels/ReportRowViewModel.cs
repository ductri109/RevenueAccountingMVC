using System.ComponentModel.DataAnnotations;

namespace RevenueAccountingMVC.ViewModels
{
    public class ReportRowViewModel
    {
        public DateTime Date { get; set; }
        public string VoucherCode { get; set; }
        public string Description { get; set; }
        public string CorrespondingAccount { get; set; }

        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        public decimal Balance { get; set; }
    }                                                                                
}
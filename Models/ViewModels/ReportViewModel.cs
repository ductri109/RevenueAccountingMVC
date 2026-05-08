using System.ComponentModel.DataAnnotations;

namespace RevenueAccountingMVC.ViewModels
{
    public class ReportViewModel
    {
        public int AccountId { get; set; }
        public string AccountNumber { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public decimal OpeningBalance { get; set; }

        public List<ReportRowViewModel> Rows { get; set; } = new();

        public decimal TotalDebit => Rows.Sum(x => x.Debit);
        public decimal TotalCredit => Rows.Sum(x => x.Credit);
        public decimal ClosingBalance => OpeningBalance + TotalDebit - TotalCredit;
    }
}
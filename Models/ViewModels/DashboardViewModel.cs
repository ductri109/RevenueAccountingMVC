using System;
using System.Collections.Generic;

namespace RevenueAccountingMVC.ViewModels
{
    public class DashboardViewModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? CustomerId { get; set; }

        // KPIs
        public decimal TotalRevenue { get; set; }
        public int TotalTransactions { get; set; }
        public int TotalCustomers { get; set; } // Mới
        public int TotalProducts { get; set; }  // Mới

        // Biểu đồ
        public List<ChartItem> TopCustomers { get; set; } = new List<ChartItem>();
        // Thay thế dòng TopProducts cũ bằng 2 dòng này
        public List<ChartItem> TopGoods { get; set; } = new List<ChartItem>();
        public List<ChartItem> TopServices { get; set; } = new List<ChartItem>();
        public List<ChartItem> RevenueOverTime { get; set; } = new List<ChartItem>(); // Mới (Biểu đồ đường)

        public bool HasData => TotalRevenue > 0;
    }

    public class ChartItem
    {
        public string Label { get; set; }
        public decimal Value { get; set; }
    }
}
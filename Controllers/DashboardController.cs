using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using RevenueAccountingMVC.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RevenueAccountingMVC.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? customerId)
        {
            // Mặc định lấy dữ liệu tháng hiện tại
            if (!fromDate.HasValue) fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!toDate.HasValue) toDate = DateTime.Now.Date;

            var model = new DashboardViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                CustomerId = customerId
            };

            // Load danh sách khách hàng cho bộ lọc
            ViewBag.Customers = await _context.Customers
                .Where(c => c.Status == CustomerStatus.Active)
                .OrderBy(c => c.CustomerName)
                .ToListAsync();

            // Luồng A3: Bộ lọc không hợp lệ
            if (fromDate > toDate)
            {
                ModelState.AddModelError("", "Khoảng thời gian không hợp lệ (Từ ngày phải <= Đến ngày)");
                return View(model);
            }

            // Luồng chính: Truy vấn dữ liệu từ chứng từ bán hàng đã Ghi sổ
            var query = _context.SalesVoucherDetails
                .Include(d => d.SalesVoucher).ThenInclude(v => v.Customer)
                .Include(d => d.Product)
                .Where(d => d.SalesVoucher.AccountingDate >= fromDate.Value 
                         && d.SalesVoucher.AccountingDate <= toDate.Value 
                         && d.SalesVoucher.Status == VoucherStatus.Posted);

            if (customerId.HasValue && customerId.Value > 0)
            {
                query = query.Where(d => d.SalesVoucher.CustomerId == customerId.Value);
            }

            var salesData = await query.ToListAsync();

            if (salesData.Any())
            {
                // Tính KPI
                model.TotalRevenue = salesData.Sum(x => x.Amount);
                model.TotalTransactions = salesData.Select(x => x.SalesVoucherId).Distinct().Count();
                model.TotalCustomers = salesData.Select(x => x.SalesVoucher.CustomerId).Distinct().Count();
                model.TotalProducts = salesData.Select(x => x.ProductId).Distinct().Count();

                // Biểu đồ 1: Top 10 Khách hàng
                model.TopCustomers = salesData
                    .GroupBy(x => x.SalesVoucher.Customer?.CustomerName ?? "Khách lẻ")
                    .Select(g => new ChartItem { Label = g.Key, Value = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.Value)
                    .Take(10)
                    .ToList();

                // Biểu đồ 2: Top 10 Mặt hàng               
                // Top 10 Sản phẩm (Hàng hóa)
                model.TopGoods = salesData
                    .Where(x => x.Product != null && x.Product.ProductType.ToString() != "Service") // Không phải dịch vụ
                    .GroupBy(x => x.Product?.ProductName ?? "Sản phẩm khác")
                    .Select(g => new ChartItem { Label = g.Key, Value = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.Value)
                    .Take(10)
                    .ToList();

                // Top 10 Dịch vụ
                model.TopServices = salesData
                    .Where(x => x.Product != null && x.Product.ProductType.ToString() == "Service") // Là dịch vụ
                    .GroupBy(x => x.Product?.ProductName ?? "Dịch vụ khác")
                    .Select(g => new ChartItem { Label = g.Key, Value = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.Value)
                    .Take(10)
                    .ToList();

                // Biểu đồ 3: Doanh thu theo thời gian (Line chart)
                model.RevenueOverTime = salesData
                    .GroupBy(x => x.SalesVoucher.AccountingDate.ToString("dd/MM"))
                    .Select(g => new ChartItem { Label = g.Key, Value = g.Sum(x => x.Amount) })
                    .OrderBy(x => x.Label) // Sắp xếp theo ngày
                    .ToList();
            }

            return View(model);
        }
    }
}
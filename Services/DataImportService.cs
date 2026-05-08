using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Data;
using RevenueAccountingMVC.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RevenueAccountingMVC.Services
{
    public class DataImportService
    {
        private readonly ApplicationDbContext _context;

        public DataImportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> ImportAllAsync(string folderPath)
        {
            var results = new List<string>();

            try
            {
                // ĐÃ BỎ HÀM ClearAllDataAsync() ĐỂ KHÔNG BỊ MẤT DỮ LIỆU CŨ

                // 1. Import Accounts
                var accountResult = await ImportAccountsAsync(folderPath);
                results.Add(accountResult);

                // 2. Import Taxes
                var taxResult = await ImportTaxesAsync(folderPath);
                results.Add(taxResult);

                // 3. Import Customers
                var customerResult = await ImportCustomersAsync(folderPath);
                results.Add(customerResult);

                // 4. Import Products
                var productResult = await ImportProductsAsync(folderPath);
                results.Add(productResult);

                return string.Join("\n", results);
            }
            catch (Exception ex)
            {
                return $"Lỗi: {ex.Message}";
            }
        }

        private async Task<string> ImportAccountsAsync(string folderPath)
        {
            var filePath = Path.Combine(folderPath, "Accounts.csv");
            if (!File.Exists(filePath))
                return "Accounts.csv không tìm thấy";

            // Lấy danh sách các Mã Tài Khoản ĐÃ CÓ trong DB đưa vào HashSet để check siêu nhanh
            var existingCodes = new HashSet<string>(await _context.Accounts.Select(a => a.AccountNumber).ToListAsync());

            var lines = File.ReadAllLines(filePath).Skip(1); // Skip header
            var count = 0;
            var skipCount = 0;

            foreach (var line in lines)
            {
                var fields = ParseCsvLine(line);
                if (fields.Length < 8) continue;

                string accNumber = fields[0];

                // Nếu mã đã tồn tại -> Bỏ qua không import
                if (existingCodes.Contains(accNumber))
                {
                    skipCount++;
                    continue;
                }

                var account = new Account
                {
                    AccountNumber = accNumber,
                    AccountName = fields[1],
                    Category = (AccountCategory)int.Parse(fields[2]),
                    Nature = (AccountNature)int.Parse(fields[3]),
                    IsDetail = bool.Parse(fields[4]),
                    Status = (AccountStatus)int.Parse(fields[5]),
                    ParentAccountId = string.IsNullOrEmpty(fields[6]) ? null : int.Parse(fields[6]),
                    Description = fields[7],
                    CreatedAt = DateTime.Now
                };

                _context.Accounts.Add(account);
                existingCodes.Add(accNumber); // Thêm vào set để chống trùng lặp ngay trong chính file CSV
                count++;
            }

            if (count > 0) await _context.SaveChangesAsync();
            return $"✓ Tài khoản: Thêm mới {count} - Bỏ qua trùng lặp {skipCount}";
        }

        private async Task<string> ImportTaxesAsync(string folderPath)
        {
            var filePath = Path.Combine(folderPath, "Taxes.csv");
            if (!File.Exists(filePath))
                return "Taxes.csv không tìm thấy";

            var existingCodes = new HashSet<string>(await _context.Taxes.Select(t => t.TaxCode).ToListAsync());

            var lines = File.ReadAllLines(filePath).Skip(1);
            var count = 0;
            var skipCount = 0;

            foreach (var line in lines)
            {
                var fields = ParseCsvLine(line);
                if (fields.Length < 7) continue;

                string taxCode = fields[0];

                if (existingCodes.Contains(taxCode))
                {
                    skipCount++;
                    continue;
                }

                var tax = new Tax
                {
                    TaxCode = taxCode,
                    TaxName = fields[1],
                    SecondaryName = fields[2],
                    TaxRate = decimal.Parse(fields[3]),
                    IsDeductible = bool.Parse(fields[4]),
                    TaxAccountId = int.Parse(fields[5]),
                    Status = (TaxStatus)int.Parse(fields[6]),
                    CreatedAt = DateTime.Now
                };

                _context.Taxes.Add(tax);
                existingCodes.Add(taxCode);
                count++;
            }

            if (count > 0) await _context.SaveChangesAsync();
            return $"✓ Thuế: Thêm mới {count} - Bỏ qua trùng lặp {skipCount}";
        }

        private async Task<string> ImportCustomersAsync(string folderPath)
        {
            var filePath = Path.Combine(folderPath, "Customers.csv");
            if (!File.Exists(filePath))
                return "Customers.csv không tìm thấy";

            var existingCodes = new HashSet<string>(await _context.Customers.Select(c => c.CustomerCode).ToListAsync());

            var lines = File.ReadAllLines(filePath).Skip(1);
            var count = 0;
            var skipCount = 0;

            foreach (var line in lines)
            {
                var fields = ParseCsvLine(line);
                if (fields.Length < 11) continue;

                string cusCode = fields[0];

                if (existingCodes.Contains(cusCode))
                {
                    skipCount++;
                    continue;
                }

                var customer = new Customer
                {
                    CustomerCode = cusCode,
                    CustomerName = fields[1],
                    CustomerType = (CustomerType)int.Parse(fields[2]),
                    Address = fields[3],
                    PhoneNumber = fields[4],
                    Email = fields[5],
                    TaxCode = string.IsNullOrEmpty(fields[6]) ? null : fields[6],
                    ReceivableAccountId = string.IsNullOrEmpty(fields[7]) ? null : int.Parse(fields[7]),
                    MaxDebtDays = string.IsNullOrEmpty(fields[8]) ? null : int.Parse(fields[8]),
                    ContactPerson = fields[9],
                    Status = (CustomerStatus)int.Parse(fields[10]),
                    CreatedAt = DateTime.Now
                };

                _context.Customers.Add(customer);
                existingCodes.Add(cusCode);
                count++;
            }

            if (count > 0) await _context.SaveChangesAsync();
            return $"✓ Khách hàng: Thêm mới {count} - Bỏ qua trùng lặp {skipCount}";
        }

        private async Task<string> ImportProductsAsync(string folderPath)
        {
            var filePath = Path.Combine(folderPath, "Products.csv");
            if (!File.Exists(filePath))
                return "Products.csv không tìm thấy";

            var existingCodes = new HashSet<string>(await _context.Products.Select(p => p.ProductCode).ToListAsync());

            var lines = File.ReadAllLines(filePath).Skip(1);
            var count = 0;
            var skipCount = 0;

            foreach (var line in lines)
            {
                var fields = ParseCsvLine(line);
                if (fields.Length < 11) continue;

                string prodCode = fields[0];

                if (existingCodes.Contains(prodCode))
                {
                    skipCount++;
                    continue;
                }

                var product = new Product
                {
                    ProductCode = prodCode,
                    ProductName = fields[1],
                    ProductType = (ProductType)int.Parse(fields[2]),
                    Unit = fields[3],
                    Status = (ProductStatus)int.Parse(fields[4]),
                    RevenueAccountId = string.IsNullOrEmpty(fields[5]) ? null : int.Parse(fields[5]),
                    InventoryAccountId = string.IsNullOrEmpty(fields[6]) ? null : int.Parse(fields[6]),
                    TaxId = string.IsNullOrEmpty(fields[7]) ? null : int.Parse(fields[7]),
                    DefaultUnitPrice = string.IsNullOrEmpty(fields[8]) ? null : decimal.Parse(fields[8]),
                    DefaultTaxRate = string.IsNullOrEmpty(fields[9]) ? null : decimal.Parse(fields[9]),
                    Description = fields[10],
                    CreatedAt = DateTime.Now
                };

                _context.Products.Add(product);
                existingCodes.Add(prodCode);
                count++;
            }

            if (count > 0) await _context.SaveChangesAsync();
            return $"✓ Mặt hàng: Thêm mới {count} - Bỏ qua trùng lặp {skipCount}";
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = "";
            var inQuotes = false;

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.Trim());
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current.Trim());

            // Xử lý giá trị NULL (từ khóa NULL trong CSV)
            return result.Select(s => s.ToUpper() == "NULL" ? "" : s).ToArray();
        }
    }
}
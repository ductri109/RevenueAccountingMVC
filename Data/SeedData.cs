using Microsoft.EntityFrameworkCore;
using RevenueAccountingMVC.Models;

namespace RevenueAccountingMVC.Data
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
            {
                // Kiểm tra xem bảng Users đã có dữ liệu chưa
                if (context.Users.Any())
                {
                    return; // Nếu có rồi thì bỏ qua, không seed nữa
                }

                // Nếu chưa có, tạo danh sách tài khoản mặc định
                var users = new User[]
                {
                    new User
                    {
                        FullName = "Quản trị viên hệ thống",
                        Email = "admin@system.com",
                        // Mật khẩu mặc định: Admin@123
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"), 
                        Role = UserRole.Admin,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        FullName = "Giám đốc tài chính",
                        Email = "leader@system.com",
                        // Mật khẩu mặc định: Leader@123
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Leader@123"), 
                        Role = UserRole.Leader,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    }
                };

                context.Users.AddRange(users);
                context.SaveChanges();
            }
        }
    }
}
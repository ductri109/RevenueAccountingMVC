using System.ComponentModel.DataAnnotations;

namespace RevenueAccountingMVC.Models
{
    public enum UserRole
    {
        [Display(Name = "Quản trị viên")]
        Admin = 1,
        
        [Display(Name = "Kế toán")]
        Accountant = 2,
        
        [Display(Name = "Lãnh đạo")]
        Leader = 3
    }

    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; } // Trong thực tế cần băm (Hash) mật khẩu

        [Required]
        public UserRole Role { get; set; }

        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
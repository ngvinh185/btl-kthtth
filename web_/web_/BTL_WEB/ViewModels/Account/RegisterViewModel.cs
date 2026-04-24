using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Account;

public class RegisterViewModel
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [Display(Name = "Ten dang nhap")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Ho va ten")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    [Display(Name = "So dien thoai")]
    public string? Phone { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "Mat khau")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mat khau xac nhan khong khop.")]
    [Display(Name = "Nhap lai mat khau")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

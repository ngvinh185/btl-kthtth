using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Account;

public class LoginViewModel
{
    [Required]
    [Display(Name = "Ten dang nhap")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Mat khau")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nho dang nhap")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

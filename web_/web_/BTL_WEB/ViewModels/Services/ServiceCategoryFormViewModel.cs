using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Services;

public class ServiceCategoryFormViewModel
{
    public int? CategoryId { get; set; }

    [Required]
    [StringLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }
}

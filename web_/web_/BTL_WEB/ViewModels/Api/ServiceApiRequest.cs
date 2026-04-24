using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Api;

public class ServiceApiRequest
{
    [Required]
    public int CategoryId { get; set; }

    [Required]
    [StringLength(100)]
    public string ServiceName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(1, int.MaxValue)]
    public int DurationMinutes { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active";
}

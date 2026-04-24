using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Adoption;

public class ReviewAdoptionRequestViewModel
{
    [Required]
    public int RequestId { get; set; }

    [Required]
    public string Action { get; set; } = "Approve";

    [Range(0, double.MaxValue)]
    public decimal? AdoptionFee { get; set; }

    [StringLength(1000)]
    public string? Terms { get; set; }
}

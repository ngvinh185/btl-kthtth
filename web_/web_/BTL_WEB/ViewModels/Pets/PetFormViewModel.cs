using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTL_WEB.ViewModels.Pets;

public class PetFormViewModel
{
    public int? PetId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Species { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Breed { get; set; }

    [StringLength(10)]
    public string? Gender { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(50)]
    public string? Color { get; set; }

    [Range(0, 1000)]
    public decimal? Weight { get; set; }

    public string? Description { get; set; }

    [StringLength(100)]
    public string? HealthStatus { get; set; }

    [StringLength(100)]
    public string? VaccinationStatus { get; set; }

    [Required]
    [StringLength(30)]
    public string AdoptionStatus { get; set; } = "Available";

    [Required]
    public int BranchId { get; set; }

    public int? OwnerId { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active";

    public List<IFormFile> ImageFiles { get; set; } = new();

    public List<SelectListItem> BranchOptions { get; set; } = new();

    public List<SelectListItem> OwnerOptions { get; set; } = new();
}

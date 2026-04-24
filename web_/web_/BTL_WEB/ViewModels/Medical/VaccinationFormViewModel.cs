using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTL_WEB.ViewModels.Medical;

public class VaccinationFormViewModel
{
    public int? VaccinationId { get; set; }

    [Required]
    public int PetId { get; set; }

    [Required]
    [StringLength(100)]
    public string VaccineName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateOnly VaccinationDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [DataType(DataType.Date)]
    public DateOnly? NextDueDate { get; set; }

    [Required]
    public int StaffId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<SelectListItem> PetOptions { get; set; } = new();

    public List<SelectListItem> StaffOptions { get; set; } = new();
}

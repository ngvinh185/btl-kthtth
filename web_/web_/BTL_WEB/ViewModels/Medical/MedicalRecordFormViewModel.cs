using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTL_WEB.ViewModels.Medical;

public class MedicalRecordFormViewModel
{
    public int? RecordId { get; set; }

    [Required]
    public int PetId { get; set; }

    [Required]
    public int StaffId { get; set; }

    [Required]
    public DateTime VisitDate { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? Diagnosis { get; set; }

    [StringLength(500)]
    public string? Treatment { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<SelectListItem> PetOptions { get; set; } = new();

    public List<SelectListItem> StaffOptions { get; set; } = new();
}

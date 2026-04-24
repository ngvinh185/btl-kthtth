using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTL_WEB.ViewModels.Appointments;

public class AppointmentCreateViewModel
{
    public int? AppointmentId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int PetId { get; set; }

    [Required]
    public int BranchId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    public DateTime AppointmentDateTime { get; set; } = DateTime.Now.AddDays(1);

    public string? Notes { get; set; }

    [MinLength(1, ErrorMessage = "Vui long chon it nhat mot dich vu.")]
    public List<int> SelectedServiceIds { get; set; } = new();

    public List<SelectListItem> UserOptions { get; set; } = new();

    public List<SelectListItem> PetOptions { get; set; } = new();

    public List<SelectListItem> BranchOptions { get; set; } = new();

    public List<SelectListItem> ServiceOptions { get; set; } = new();
}

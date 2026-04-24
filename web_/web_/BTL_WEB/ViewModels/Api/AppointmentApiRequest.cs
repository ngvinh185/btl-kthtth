using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Api;

public class AppointmentApiRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public int PetId { get; set; }

    [Required]
    public int BranchId { get; set; }

    [Required]
    public DateTime AppointmentDateTime { get; set; }

    public string? Notes { get; set; }

    [MinLength(1)]
    public List<int> SelectedServiceIds { get; set; } = new();
}

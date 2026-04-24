using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Appointments;

public class AppointmentStatusUpdateViewModel
{
    [Required]
    public int AppointmentId { get; set; }

    [Required]
    public string Status { get; set; } = "Pending";
}

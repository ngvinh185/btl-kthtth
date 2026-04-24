using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Api;

public class AppointmentStatusApiRequest
{
    [Required]
    public string Status { get; set; } = "Pending";
}

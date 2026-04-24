using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTL_WEB.ViewModels.Adoption;

public class CreateAdoptionRequestViewModel
{
    [Required]
    public int PetId { get; set; }

    [StringLength(500)]
    public string? Message { get; set; }

    public List<SelectListItem> PetOptions { get; set; } = new();
}

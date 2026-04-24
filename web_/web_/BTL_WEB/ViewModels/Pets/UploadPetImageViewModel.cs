using System.ComponentModel.DataAnnotations;

namespace BTL_WEB.ViewModels.Pets;

public class UploadPetImageViewModel
{
    [Required]
    public int PetId { get; set; }

    [MinLength(1, ErrorMessage = "Vui long chon it nhat mot anh.")]
    public List<IFormFile> ImageFiles { get; set; } = new();
}

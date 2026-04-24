using BTL_WEB.Models;
using Microsoft.AspNetCore.Http;

namespace BTL_WEB.Services;

public interface IPetService
{
    Task<List<PetImage>> SaveImagesAsync(int petId, IEnumerable<IFormFile> imageFiles);
}

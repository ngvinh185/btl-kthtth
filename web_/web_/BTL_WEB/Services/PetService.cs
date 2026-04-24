using BTL_WEB.Models;

namespace BTL_WEB.Services;

public class PetService : IPetService
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    private readonly IWebHostEnvironment _environment;

    public PetService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<List<PetImage>> SaveImagesAsync(int petId, IEnumerable<IFormFile> imageFiles)
    {
        var savedImages = new List<PetImage>();
        var files = imageFiles.Where(x => x is not null && x.Length > 0).ToList();

        if (files.Count == 0)
        {
            return savedImages;
        }

        var uploadRoot = Path.Combine(_environment.WebRootPath, "images", "pets");
        Directory.CreateDirectory(uploadRoot);

        foreach (var file in files)
        {
            if (file.Length > MaxImageSizeBytes)
            {
                throw new InvalidOperationException("Anh tai len khong duoc vuot qua 5MB.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Chi cho phep anh jpg, jpeg hoac png.");
            }

            if (!IsAllowedImageContent(file, extension))
            {
                throw new InvalidOperationException("Noi dung file khong dung dinh dang anh hop le.");
            }

            var uniqueName = $"{Guid.NewGuid():N}{extension}";
            var destination = Path.Combine(uploadRoot, uniqueName);

            await using var stream = new FileStream(destination, FileMode.Create);
            await file.CopyToAsync(stream);

            savedImages.Add(new PetImage
            {
                PetId = petId,
                ImageUrl = $"/images/pets/{uniqueName}",
                IsPrimary = savedImages.Count == 0
            });
        }

        return savedImages;
    }

    private static bool IsAllowedImageContent(IFormFile file, string extension)
    {
        var contentTypeAllowed = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase)
            : string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(file.ContentType, "image/pjpeg", StringComparison.OrdinalIgnoreCase);

        if (!contentTypeAllowed)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[8];
        using var stream = file.OpenReadStream();
        var read = stream.Read(header);

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return read >= 8
                && header[0] == 0x89
                && header[1] == 0x50
                && header[2] == 0x4E
                && header[3] == 0x47
                && header[4] == 0x0D
                && header[5] == 0x0A
                && header[6] == 0x1A
                && header[7] == 0x0A;
        }

        return read >= 3
            && header[0] == 0xFF
            && header[1] == 0xD8
            && header[2] == 0xFF;
    }
}

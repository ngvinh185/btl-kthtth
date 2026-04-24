using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Services;
using BTL_WEB.ViewModels.Pets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.AllRoles)]
public class PetsController : Controller
{
    private readonly PetCareHubContext _context;
    private readonly IPetService _petService;

    public PetsController(PetCareHubContext context, IPetService petService)
    {
        _context = context;
        _petService = petService;
    }

    public async Task<IActionResult> Index(string? name, string? species, int? branchId, string? adoptionStatus, int page = 1, int pageSize = 10)
    {
        var query = _context.Pets
            .Include(x => x.Branch)
            .Include(x => x.Owner)
            .Include(x => x.PetImages)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(x => x.Name.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(species))
        {
            query = query.Where(x => x.Species.Contains(species));
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(adoptionStatus))
        {
            query = query.Where(x => x.AdoptionStatus == adoptionStatus);
        }

        ViewBag.Name = name;
        ViewBag.Species = species;
        ViewBag.BranchId = branchId;
        ViewBag.AdoptionStatus = adoptionStatus;
        ViewBag.PageSize = pageSize;
        ViewBag.Branches = await _context.Branches
            .OrderBy(x => x.BranchName)
            .Select(x => new SelectListItem(x.BranchName, x.BranchId.ToString()))
            .ToListAsync();

        var pagedPets = await PaginatedList<Pet>.CreateAsync(query.OrderByDescending(x => x.CreatedAt), page, pageSize);
        return View(pagedPets);
    }

    public async Task<IActionResult> Details(int id)
    {
        var pet = await _context.Pets
            .Include(x => x.Branch)
            .Include(x => x.Owner)
            .Include(x => x.PetImages)
            .Include(x => x.MedicalRecords)
                .ThenInclude(x => x.Staff)
                    .ThenInclude(x => x.User)
            .Include(x => x.Vaccinations)
                .ThenInclude(x => x.Staff)
                    .ThenInclude(x => x.User)
            .Include(x => x.Appointments)
                .ThenInclude(x => x.AppointmentServices)
                    .ThenInclude(x => x.Service)
            .FirstOrDefaultAsync(x => x.PetId == id);

        if (pet is null)
        {
            return NotFound();
        }

        var currentUserId = User.GetUserId();
        if (User.IsInRole(RoleNames.Customer) && pet.OwnerId != currentUserId)
        {
            return Forbid();
        }

        ViewBag.UploadModel = new UploadPetImageViewModel { PetId = id };
        return View(pet);
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Create()
    {
        return View(await BuildPetFormViewModelAsync(new PetFormViewModel()));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PetFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildPetFormViewModelAsync(model));
        }

        var pet = new Pet
        {
            Name = model.Name,
            Species = model.Species,
            Breed = model.Breed,
            Gender = model.Gender,
            DateOfBirth = model.DateOfBirth,
            Color = model.Color,
            Weight = model.Weight,
            Description = model.Description,
            HealthStatus = model.HealthStatus,
            VaccinationStatus = model.VaccinationStatus,
            AdoptionStatus = model.AdoptionStatus,
            BranchId = model.BranchId,
            OwnerId = model.OwnerId,
            Status = model.Status,
            CreatedAt = DateTime.Now
        };

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync();

        await SaveUploadedImagesAsync(pet.PetId, model.ImageFiles);
        return RedirectToAction("Pets", "Management");
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Edit(int id)
    {
        var pet = await _context.Pets.FindAsync(id);
        if (pet is null)
        {
            return NotFound();
        }

        var model = new PetFormViewModel
        {
            PetId = pet.PetId,
            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed,
            Gender = pet.Gender,
            DateOfBirth = pet.DateOfBirth,
            Color = pet.Color,
            Weight = pet.Weight,
            Description = pet.Description,
            HealthStatus = pet.HealthStatus,
            VaccinationStatus = pet.VaccinationStatus,
            AdoptionStatus = pet.AdoptionStatus,
            BranchId = pet.BranchId,
            OwnerId = pet.OwnerId,
            Status = pet.Status
        };

        return View(await BuildPetFormViewModelAsync(model));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PetFormViewModel model)
    {
        if (id != model.PetId)
        {
            return NotFound();
        }

        var pet = await _context.Pets.FindAsync(id);
        if (pet is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildPetFormViewModelAsync(model));
        }

        pet.Name = model.Name;
        pet.Species = model.Species;
        pet.Breed = model.Breed;
        pet.Gender = model.Gender;
        pet.DateOfBirth = model.DateOfBirth;
        pet.Color = model.Color;
        pet.Weight = model.Weight;
        pet.Description = model.Description;
        pet.HealthStatus = model.HealthStatus;
        pet.VaccinationStatus = model.VaccinationStatus;
        pet.AdoptionStatus = model.AdoptionStatus;
        pet.BranchId = model.BranchId;
        pet.OwnerId = model.OwnerId;
        pet.Status = model.Status;

        await SaveUploadedImagesAsync(pet.PetId, model.ImageFiles);
        await _context.SaveChangesAsync();

        return RedirectToAction("Pets", "Management");
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var pet = await _context.Pets
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.PetId == id);

        if (pet is null)
        {
            return NotFound();
        }

        return View(pet);
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var pet = await _context.Pets.FindAsync(id);
        if (pet is null)
        {
            return NotFound();
        }

        _context.Pets.Remove(pet);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImages(UploadPetImageViewModel model)
    {
        var petExists = await _context.Pets.AnyAsync(x => x.PetId == model.PetId);
        if (!petExists)
        {
            if (Request.IsAjaxRequest())
            {
                return NotFound(new { success = false, message = "Khong tim thay pet." });
            }

            return NotFound();
        }

        if (model.ImageFiles.Count == 0)
        {
            if (Request.IsAjaxRequest())
            {
                return BadRequest(new { success = false, message = "Vui long chon it nhat mot anh." });
            }

            TempData["Error"] = "Vui long chon it nhat mot anh.";
            return RedirectToAction(nameof(Details), new { id = model.PetId });
        }

        try
        {
            await SaveUploadedImagesAsync(model.PetId, model.ImageFiles);
        }
        catch (InvalidOperationException ex)
        {
            if (Request.IsAjaxRequest())
            {
                return BadRequest(new { success = false, message = ex.Message });
            }

            TempData["Error"] = ex.Message;
        }

        if (Request.IsAjaxRequest())
        {
            var images = await _context.PetImages
                .Where(x => x.PetId == model.PetId)
                .OrderByDescending(x => x.UploadedAt)
                .Select(x => new { x.ImageId, x.ImageUrl, x.IsPrimary })
                .ToListAsync();

            return Json(new
            {
                success = true,
                message = "Upload anh thanh cong.",
                images
            });
        }

        return RedirectToAction(nameof(Details), new { id = model.PetId });
    }

    private async Task<PetFormViewModel> BuildPetFormViewModelAsync(PetFormViewModel model)
    {
        model.BranchOptions = await _context.Branches
            .OrderBy(x => x.BranchName)
            .Select(x => new SelectListItem(x.BranchName, x.BranchId.ToString()))
            .ToListAsync();

        model.OwnerOptions = await _context.Users
            .Include(x => x.Role)
            .Where(x => x.Role.RoleName == RoleNames.Customer && x.Status == "Active")
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.UserId.ToString()))
            .ToListAsync();

        model.OwnerOptions.Insert(0, new SelectListItem("Khong co owner", string.Empty));
        return model;
    }

    private async Task SaveUploadedImagesAsync(int petId, IEnumerable<IFormFile> imageFiles)
    {
        var images = await _petService.SaveImagesAsync(petId, imageFiles);
        if (images.Count == 0)
        {
            return;
        }

        var hasExistingPrimary = await _context.PetImages.AnyAsync(x => x.PetId == petId && x.IsPrimary);
        if (hasExistingPrimary)
        {
            images[0].IsPrimary = false;
        }

        _context.PetImages.AddRange(images);
        await _context.SaveChangesAsync();
    }
}

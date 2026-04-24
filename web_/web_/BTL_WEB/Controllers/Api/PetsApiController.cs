using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.ViewModels.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers.Api;

[ApiController]
[Route("api/pets")]
[Authorize(Policy = RoleNames.AllRoles)]
public class PetsApiController : ControllerBase
{
    private readonly PetCareHubContext _context;

    public PetsApiController(PetCareHubContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? name, string? species, int page = 1, int pageSize = 10)
    {
        var query = _context.Pets
            .Include(x => x.Branch)
            .Include(x => x.Owner)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(x => x.Name.Contains(name));
        }

        if (!string.IsNullOrWhiteSpace(species))
        {
            query = query.Where(x => x.Species.Contains(species));
        }

        var paged = await PaginatedList<Pet>.CreateAsync(query.OrderBy(x => x.Name), page, pageSize);
        return Ok(new
        {
            paged.PageIndex,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            items = paged.Items.Select(x => new
            {
                x.PetId,
                x.Name,
                x.Species,
                x.AdoptionStatus,
                Branch = x.Branch.BranchName,
                Owner = x.Owner != null ? x.Owner.FullName : null
            })
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var pet = await _context.Pets
            .Include(x => x.Branch)
            .Include(x => x.Owner)
            .Include(x => x.PetImages)
            .FirstOrDefaultAsync(x => x.PetId == id);

        return pet is null
            ? NotFound()
            : Ok(new
            {
                pet.PetId,
                pet.Name,
                pet.Species,
                pet.Breed,
                pet.Gender,
                pet.DateOfBirth,
                pet.Color,
                pet.Weight,
                pet.Description,
                pet.HealthStatus,
                pet.VaccinationStatus,
                pet.AdoptionStatus,
                pet.Status,
                pet.BranchId,
                pet.OwnerId,
                Branch = pet.Branch.BranchName,
                Owner = pet.Owner?.FullName,
                Images = pet.PetImages.Select(x => x.ImageUrl)
            });
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Create(PetApiRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var pet = new Pet
        {
            Name = request.Name,
            Species = request.Species,
            Breed = request.Breed,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Color = request.Color,
            Weight = request.Weight,
            Description = request.Description,
            HealthStatus = request.HealthStatus,
            VaccinationStatus = request.VaccinationStatus,
            AdoptionStatus = request.AdoptionStatus,
            BranchId = request.BranchId,
            OwnerId = request.OwnerId,
            Status = request.Status,
            CreatedAt = DateTime.Now
        };

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = pet.PetId }, new { pet.PetId, pet.Name });
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Update(int id, PetApiRequest request)
    {
        var pet = await _context.Pets.FindAsync(id);
        if (pet is null)
        {
            return NotFound();
        }

        pet.Name = request.Name;
        pet.Species = request.Species;
        pet.Breed = request.Breed;
        pet.Gender = request.Gender;
        pet.DateOfBirth = request.DateOfBirth;
        pet.Color = request.Color;
        pet.Weight = request.Weight;
        pet.Description = request.Description;
        pet.HealthStatus = request.HealthStatus;
        pet.VaccinationStatus = request.VaccinationStatus;
        pet.AdoptionStatus = request.AdoptionStatus;
        pet.BranchId = request.BranchId;
        pet.OwnerId = request.OwnerId;
        pet.Status = request.Status;

        await _context.SaveChangesAsync();
        return Ok(new { success = true, pet.PetId });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var pet = await _context.Pets.FirstOrDefaultAsync(x => x.PetId == id);
        if (pet is null)
        {
            return NotFound();
        }

        if (string.Equals(pet.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                success = true,
                softDeleted = true,
                pet.PetId
            });
        }

        pet.Status = "Inactive";
        await _context.SaveChangesAsync();
        return Ok(new
        {
            success = true,
            softDeleted = true,
            pet.PetId
        });
    }
}

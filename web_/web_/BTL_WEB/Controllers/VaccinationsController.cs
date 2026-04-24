using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.ViewModels.Medical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.StaffOrAdmin)]
public class VaccinationsController : Controller
{
    private readonly PetCareHubContext _context;

    public VaccinationsController(PetCareHubContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? petId)
    {
        var query = _context.Vaccinations
            .Include(x => x.Pet)
            .Include(x => x.Staff)
                .ThenInclude(x => x.User)
            .AsQueryable();

        if (petId.HasValue)
        {
            query = query.Where(x => x.PetId == petId.Value);
        }

        ViewBag.PetId = petId;
        return View(await query.OrderByDescending(x => x.VaccinationDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var vaccination = await _context.Vaccinations
            .Include(x => x.Pet)
            .Include(x => x.Staff)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.VaccinationId == id);

        return vaccination is null ? NotFound() : View(vaccination);
    }

    public async Task<IActionResult> Create(int? petId = null)
    {
        return View(await BuildModelAsync(new VaccinationFormViewModel { PetId = petId ?? 0 }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VaccinationFormViewModel model)
    {
        if (model.NextDueDate.HasValue && model.NextDueDate.Value < model.VaccinationDate)
        {
            ModelState.AddModelError(nameof(model.NextDueDate), "NextDueDate phai >= VaccinationDate.");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        _context.Vaccinations.Add(new Vaccination
        {
            PetId = model.PetId,
            VaccineName = model.VaccineName,
            VaccinationDate = model.VaccinationDate,
            NextDueDate = model.NextDueDate,
            StaffId = model.StaffId,
            Notes = model.Notes
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { petId = model.PetId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var vaccination = await _context.Vaccinations.FindAsync(id);
        if (vaccination is null)
        {
            return NotFound();
        }

        return View(await BuildModelAsync(new VaccinationFormViewModel
        {
            VaccinationId = vaccination.VaccinationId,
            PetId = vaccination.PetId,
            VaccineName = vaccination.VaccineName,
            VaccinationDate = vaccination.VaccinationDate,
            NextDueDate = vaccination.NextDueDate,
            StaffId = vaccination.StaffId,
            Notes = vaccination.Notes
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VaccinationFormViewModel model)
    {
        if (id != model.VaccinationId)
        {
            return NotFound();
        }

        if (model.NextDueDate.HasValue && model.NextDueDate.Value < model.VaccinationDate)
        {
            ModelState.AddModelError(nameof(model.NextDueDate), "NextDueDate phai >= VaccinationDate.");
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        var vaccination = await _context.Vaccinations.FindAsync(id);
        if (vaccination is null)
        {
            return NotFound();
        }

        vaccination.PetId = model.PetId;
        vaccination.VaccineName = model.VaccineName;
        vaccination.VaccinationDate = model.VaccinationDate;
        vaccination.NextDueDate = model.NextDueDate;
        vaccination.StaffId = model.StaffId;
        vaccination.Notes = model.Notes;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var vaccination = await _context.Vaccinations
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.VaccinationId == id);

        return vaccination is null ? NotFound() : View(vaccination);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var vaccination = await _context.Vaccinations.FindAsync(id);
        if (vaccination is null)
        {
            return NotFound();
        }

        _context.Vaccinations.Remove(vaccination);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<VaccinationFormViewModel> BuildModelAsync(VaccinationFormViewModel model)
    {
        model.PetOptions = await _context.Pets
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.PetId.ToString()))
            .ToListAsync();

        if (User.GetStaffId().HasValue)
        {
            model.StaffId = User.GetStaffId()!.Value;
            model.StaffOptions = await _context.Staff
                .Where(x => x.StaffId == model.StaffId)
                .Include(x => x.User)
                .Select(x => new SelectListItem(x.User.FullName, x.StaffId.ToString()))
                .ToListAsync();
        }
        else
        {
            model.StaffOptions = await _context.Staff
                .Include(x => x.User)
                .OrderBy(x => x.User.FullName)
                .Select(x => new SelectListItem(x.User.FullName, x.StaffId.ToString()))
                .ToListAsync();
        }

        return model;
    }
}

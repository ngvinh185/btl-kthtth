using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.ViewModels.Medical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.StaffOrAdmin)]
public class MedicalRecordsController : Controller
{
    private readonly PetCareHubContext _context;

    public MedicalRecordsController(PetCareHubContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? petId)
    {
        var query = _context.MedicalRecords
            .Include(x => x.Pet)
            .Include(x => x.Staff)
                .ThenInclude(x => x.User)
            .AsQueryable();

        if (petId.HasValue)
        {
            query = query.Where(x => x.PetId == petId.Value);
        }

        ViewBag.PetId = petId;
        return View(await query.OrderByDescending(x => x.VisitDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var record = await _context.MedicalRecords
            .Include(x => x.Pet)
            .Include(x => x.Staff)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.RecordId == id);

        return record is null ? NotFound() : View(record);
    }

    public async Task<IActionResult> Create(int? petId = null)
    {
        return View(await BuildModelAsync(new MedicalRecordFormViewModel { PetId = petId ?? 0 }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MedicalRecordFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        _context.MedicalRecords.Add(new MedicalRecord
        {
            PetId = model.PetId,
            StaffId = model.StaffId,
            VisitDate = model.VisitDate,
            Diagnosis = model.Diagnosis,
            Treatment = model.Treatment,
            Notes = model.Notes
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { petId = model.PetId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var record = await _context.MedicalRecords.FindAsync(id);
        if (record is null)
        {
            return NotFound();
        }

        return View(await BuildModelAsync(new MedicalRecordFormViewModel
        {
            RecordId = record.RecordId,
            PetId = record.PetId,
            StaffId = record.StaffId,
            VisitDate = record.VisitDate,
            Diagnosis = record.Diagnosis,
            Treatment = record.Treatment,
            Notes = record.Notes
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MedicalRecordFormViewModel model)
    {
        if (id != model.RecordId)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        var record = await _context.MedicalRecords.FindAsync(id);
        if (record is null)
        {
            return NotFound();
        }

        record.PetId = model.PetId;
        record.StaffId = model.StaffId;
        record.VisitDate = model.VisitDate;
        record.Diagnosis = model.Diagnosis;
        record.Treatment = model.Treatment;
        record.Notes = model.Notes;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var record = await _context.MedicalRecords
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.RecordId == id);

        return record is null ? NotFound() : View(record);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var record = await _context.MedicalRecords.FindAsync(id);
        if (record is null)
        {
            return NotFound();
        }

        _context.MedicalRecords.Remove(record);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<MedicalRecordFormViewModel> BuildModelAsync(MedicalRecordFormViewModel model)
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

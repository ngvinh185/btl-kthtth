using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.ViewModels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.StaffOrAdmin)]
public class ServiceCategoriesController : Controller
{
    private readonly PetCareHubContext _context;

    public ServiceCategoriesController(PetCareHubContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.ServiceCategories
            .Include(x => x.Services)
            .OrderBy(x => x.CategoryName)
            .ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var category = await _context.ServiceCategories
            .Include(x => x.Services)
            .FirstOrDefaultAsync(x => x.CategoryId == id);

        return category is null ? NotFound() : View(category);
    }

    public IActionResult Create()
    {
        return View(new ServiceCategoryFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceCategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _context.ServiceCategories.Add(new ServiceCategory
        {
            CategoryName = model.CategoryName,
            Description = model.Description
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        return View(new ServiceCategoryFormViewModel
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            Description = category.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceCategoryFormViewModel model)
    {
        if (id != model.CategoryId)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = await _context.ServiceCategories.FindAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        category.CategoryName = model.CategoryName;
        category.Description = model.Description;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);
        return category is null ? NotFound() : View(category);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);
        if (category is null)
        {
            return NotFound();
        }

        _context.ServiceCategories.Remove(category);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Models.ViewModels;
using BTL_WEB.ViewModels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.StaffOrAdmin)]
public class ServicesController : Controller
{
    private readonly PetCareHubContext _context;

    public ServicesController(PetCareHubContext context)
    {
        _context = context;
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Index(string? keyword, int? categoryId, string? priceRange, string? status, int page = 1, int pageSize = 10)
    {
        var query = _context.Services
            .Include(x => x.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.ServiceName.Contains(keyword) || (x.Description ?? string.Empty).Contains(keyword));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(priceRange))
        {
            query = priceRange switch
            {
                "10000-100000" => query.Where(x => x.Price >= 10000m && x.Price < 100000m),
                "100000-500000" => query.Where(x => x.Price >= 100000m && x.Price < 500000m),
                "500000-plus" => query.Where(x => x.Price >= 500000m),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        ViewBag.Keyword = keyword;
        ViewBag.CategoryId = categoryId;
        ViewBag.PriceRange = priceRange;
        ViewBag.Status = status;
        ViewBag.PageSize = pageSize;
        ViewBag.Categories = await _context.ServiceCategories
            .OrderBy(x => x.CategoryName)
            .Select(x => new SelectListItem(x.CategoryName, x.CategoryId.ToString()))
            .ToListAsync();

        return View(await PaginatedList<Service>.CreateAsync(query.OrderBy(x => x.ServiceName), page, pageSize));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Details(int id)
    {
        var service = await _context.Services
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.ServiceId == id);

        return service is null ? NotFound() : View(service);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Catalog()
    {
        ViewBag.BranchOptions = await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.BranchName)
            .Select(b => new { b.BranchId, b.BranchName })
            .ToListAsync();

        ViewBag.AllPetOptions = await _context.Pets
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.PetId,
                p.OwnerId,
                p.Name,
                p.Species,
                p.Breed,
                p.Gender
            })
            .ToListAsync();

        var currentUserId = await ResolveCurrentUserIdAsync();

        if (currentUserId.HasValue)
        {
            var customerPetOptions = await _context.Pets
                .AsNoTracking()
                .Where(p => p.OwnerId == currentUserId.Value)
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.PetId,
                    p.OwnerId,
                    p.Name,
                    p.Species,
                    p.Breed,
                    p.Gender
                })
                .ToListAsync();

            ViewBag.CustomerPetOptions = customerPetOptions;
        }
        else
        {
            ViewBag.CustomerPetOptions = new List<object>();
        }

        var groups = await _context.ServiceCategories
            .AsNoTracking()
            .OrderBy(x => x.CategoryName)
            .Select(x => new ServiceCategoryGroupViewModel
            {
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryName,
                Services = x.Services
                    .Where(s => s.Status == "Active")
                    .OrderBy(s => s.ServiceName)
                    .Select(s => new ServiceCardViewModel
                    {
                        ServiceId = s.ServiceId,
                        ServiceName = s.ServiceName,
                        Description = s.Description,
                        Price = s.Price,
                        DurationMinutes = s.DurationMinutes
                    })
                    .ToList()
            })
            .ToListAsync();

        return View(new ServiceCatalogViewModel { Groups = groups });
    }

    private async Task<int?> ResolveCurrentUserIdAsync()
    {
        var claimValue = User.FindFirstValue(ClaimNames.UserId) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claimValue, out var userId))
        {
            return userId;
        }

        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(User.Identity.Name))
        {
            return null;
        }

        var identityName = User.Identity.Name.Trim();
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Username == identityName || u.Email == identityName)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync();
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Create()
    {
        return View(await BuildModelAsync(new ServiceFormViewModel()));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        _context.Services.Add(new Service
        {
            CategoryId = model.CategoryId,
            ServiceName = model.ServiceName,
            Description = model.Description,
            Price = model.Price,
            DurationMinutes = model.DurationMinutes,
            Status = model.Status
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Edit(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service is null)
        {
            return NotFound();
        }

        return View(await BuildModelAsync(new ServiceFormViewModel
        {
            ServiceId = service.ServiceId,
            CategoryId = service.CategoryId,
            ServiceName = service.ServiceName,
            Description = service.Description,
            Price = service.Price,
            DurationMinutes = service.DurationMinutes,
            Status = service.Status
        }));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ServiceFormViewModel model)
    {
        if (id != model.ServiceId)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        var service = await _context.Services.FindAsync(id);
        if (service is null)
        {
            return NotFound();
        }

        service.CategoryId = model.CategoryId;
        service.ServiceName = model.ServiceName;
        service.Description = model.Description;
        service.Price = model.Price;
        service.DurationMinutes = model.DurationMinutes;
        service.Status = model.Status;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
    {
        var service = await _context.Services
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.ServiceId == id);

        return service is null ? NotFound() : View(service);
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var service = await _context.Services.FindAsync(id);
        if (service is null)
        {
            return NotFound();
        }

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<ServiceFormViewModel> BuildModelAsync(ServiceFormViewModel model)
    {
        model.CategoryOptions = await _context.ServiceCategories
            .OrderBy(x => x.CategoryName)
            .Select(x => new SelectListItem(x.CategoryName, x.CategoryId.ToString()))
            .ToListAsync();

        return model;
    }
}

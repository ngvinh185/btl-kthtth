using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Services;
using BTL_WEB.ViewModels.Adoption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.AllRoles)]
public class AdoptionRequestsController : Controller
{
    private readonly PetCareHubContext _context;
    private readonly IAdoptionWorkflowService _adoptionWorkflowService;

    public AdoptionRequestsController(PetCareHubContext context, IAdoptionWorkflowService adoptionWorkflowService)
    {
        _context = context;
        _adoptionWorkflowService = adoptionWorkflowService;
    }

    public async Task<IActionResult> Index(string? status, int? petId, int page = 1, int pageSize = 10)
    {
        var query = _context.AdoptionRequests
            .Include(x => x.Pet)
            .Include(x => x.User)
            .Include(x => x.ReviewedByStaff)
                .ThenInclude(x => x!.User)
            .AsQueryable();

        var currentUserId = User.GetUserId();
        if (User.IsInRole(RoleNames.Customer) && currentUserId.HasValue)
        {
            query = query.Where(x => x.UserId == currentUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (petId.HasValue)
        {
            query = query.Where(x => x.PetId == petId.Value);
        }

        ViewBag.Status = status;
        ViewBag.PetId = petId;
        ViewBag.PageSize = pageSize;
        ViewBag.Pets = await _context.Pets
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.PetId.ToString()))
            .ToListAsync();

        return View(await PaginatedList<AdoptionRequest>.CreateAsync(query.OrderByDescending(x => x.RequestDate), page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var request = await _context.AdoptionRequests
            .Include(x => x.Pet)
            .Include(x => x.User)
            .Include(x => x.AdoptionContract)
            .Include(x => x.ReviewedByStaff)
                .ThenInclude(x => x!.User)
            .FirstOrDefaultAsync(x => x.RequestId == id);

        if (request is null)
        {
            return NotFound();
        }

        if (User.IsInRole(RoleNames.Customer) && request.UserId != User.GetUserId())
        {
            return Forbid();
        }

        ViewBag.ReviewModel = new ReviewAdoptionRequestViewModel { RequestId = id };
        return View(request);
    }

    [Authorize(Roles = RoleNames.Customer)]
    public async Task<IActionResult> Create()
    {
        return View(await BuildCreateModelAsync(new CreateAdoptionRequestViewModel()));
    }

    [Authorize(Roles = RoleNames.Customer)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAdoptionRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildCreateModelAsync(model));
        }

        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        var result = await _adoptionWorkflowService.CreateRequestAsync(currentUserId.Value, model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Khong the tao adoption request.");
            return View(await BuildCreateModelAsync(model));
        }

        return RedirectToAction(nameof(Details), new { id = result.RequestId });
    }

    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewAdoptionRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (Request.IsAjaxRequest())
            {
                return BadRequest(new { success = false, message = "Du lieu review khong hop le." });
            }

            TempData["Error"] = "Du lieu review khong hop le.";
            return RedirectToAction(nameof(Details), new { id = model.RequestId });
        }

        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        var result = await _adoptionWorkflowService.ReviewRequestAsync(currentUserId.Value, model);

        if (Request.IsAjaxRequest())
        {
            return Json(new
            {
                success = result.Success,
                message = result.Success ? "Cap nhat yeu cau nhan nuoi thanh cong." : result.ErrorMessage,
                action = model.Action
            });
        }

        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Cap nhat yeu cau nhan nuoi thanh cong." : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id = model.RequestId });
    }

    private async Task<CreateAdoptionRequestViewModel> BuildCreateModelAsync(CreateAdoptionRequestViewModel model)
    {
        model.PetOptions = await _context.Pets
            .Where(x => x.AdoptionStatus == "Available" && x.Status == "Active")
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem($"{x.Name} - {x.Species}", x.PetId.ToString()))
            .ToListAsync();

        return model;
    }
}

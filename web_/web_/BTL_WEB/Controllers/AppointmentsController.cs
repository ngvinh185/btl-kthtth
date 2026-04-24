using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Services;
using BTL_WEB.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.AllRoles)]
public class AppointmentsController : Controller
{
    private readonly PetCareHubContext _context;
    private readonly IAppointmentWorkflowService _appointmentWorkflowService;

    public AppointmentsController(PetCareHubContext context, IAppointmentWorkflowService appointmentWorkflowService)
    {
        _context = context;
        _appointmentWorkflowService = appointmentWorkflowService;
    }

    public async Task<IActionResult> Index(string? status, int? branchId, string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _context.Appointments
            .Include(x => x.User)
            .Include(x => x.Pet)
            .Include(x => x.Branch)
            .Include(x => x.AppointmentServices)
                .ThenInclude(x => x.Service)
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

        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Pet.Name.Contains(keyword) ||
                x.User.FullName.Contains(keyword) ||
                (x.Notes != null && x.Notes.Contains(keyword)));
        }

        ViewBag.Status = status;
        ViewBag.BranchId = branchId;
        ViewBag.Keyword = keyword;
        ViewBag.PageSize = pageSize;
        ViewBag.Branches = await _context.Branches
            .OrderBy(x => x.BranchName)
            .Select(x => new SelectListItem(x.BranchName, x.BranchId.ToString()))
            .ToListAsync();

        return View(await PaginatedList<Appointment>.CreateAsync(query.OrderByDescending(x => x.AppointmentDateTime), page, pageSize));
    }

    public async Task<IActionResult> Details(int id)
    {
        var appointment = await _context.Appointments
            .Include(x => x.User)
            .Include(x => x.Pet)
            .Include(x => x.Branch)
            .Include(x => x.Payments)
            .Include(x => x.AppointmentServices)
                .ThenInclude(x => x.Service)
            .FirstOrDefaultAsync(x => x.AppointmentId == id);

        if (appointment is null)
        {
            return NotFound();
        }

        if (User.IsInRole(RoleNames.Customer) && appointment.UserId != User.GetUserId())
        {
            return Forbid();
        }

        ViewBag.TotalAmount = await _appointmentWorkflowService.CalculateTotalAsync(id);
        return View(appointment);
    }

    public async Task<IActionResult> Create()
    {
        return View(await BuildModelAsync(new AppointmentCreateViewModel()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (Request.IsAjaxRequest())
            {
                var firstError = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage).FirstOrDefault();
                return BadRequest(new { success = false, message = firstError ?? "Du lieu appointment khong hop le." });
            }

            return View(await BuildModelAsync(model));
        }

        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return Challenge();
        }

        var result = await _appointmentWorkflowService.CreateAppointmentAsync(
            currentUserId.Value,
            User.IsInRole(RoleNames.Customer),
            model);

        if (!result.Success)
        {
            if (Request.IsAjaxRequest())
            {
                return BadRequest(new { success = false, message = result.ErrorMessage ?? "Khong the tao lich hen." });
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Khong the tao lich hen.");
            return View(await BuildModelAsync(model));
        }

        if (Request.IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = "Tao appointment thanh cong.",
                appointmentId = result.AppointmentId,
                redirectUrl = Url.Action(nameof(Details), new { id = result.AppointmentId })
            });
        }

        return RedirectToAction(nameof(Details), new { id = result.AppointmentId });
    }

    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    public async Task<IActionResult> EditStatus(int id)
    {
        var appointment = await _context.Appointments.FindAsync(id);
        if (appointment is null)
        {
            return NotFound();
        }

        return View(new AppointmentStatusUpdateViewModel
        {
            AppointmentId = appointment.AppointmentId,
            Status = appointment.Status
        });
    }

    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStatus(AppointmentStatusUpdateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (Request.IsAjaxRequest())
            {
                return BadRequest(new { success = false, message = "Du lieu khong hop le." });
            }

            return View(model);
        }

        var result = await _appointmentWorkflowService.UpdateStatusAsync(model.AppointmentId, model.Status);
        if (!result.Success)
        {
            if (Request.IsAjaxRequest())
            {
                return BadRequest(new { success = false, message = result.ErrorMessage ?? "Cap nhat trang thai that bai." });
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cap nhat trang thai that bai.");
            return View(model);
        }

        if (Request.IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = "Cap nhat trang thai thanh cong.",
                status = model.Status,
                statusLabel = StatusDisplay.Label(model.Status)
            });
        }

        return RedirectToAction(nameof(Details), new { id = model.AppointmentId });
    }

    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    public async Task<IActionResult> Delete(int id)
    {
        var appointment = await _context.Appointments
            .Include(x => x.User)
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.AppointmentId == id);

        return appointment is null ? NotFound() : View(appointment);
    }

    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var appointment = await _context.Appointments
            .Include(x => x.AppointmentServices)
            .FirstOrDefaultAsync(x => x.AppointmentId == id);

        if (appointment is null)
        {
            return NotFound();
        }

        _context.AppointmentServices.RemoveRange(appointment.AppointmentServices);
        _context.Appointments.Remove(appointment);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<AppointmentCreateViewModel> BuildModelAsync(AppointmentCreateViewModel model)
    {
        var currentUserId = User.GetUserId();

        model.BranchOptions = await _context.Branches
            .Where(x => x.Status == "Active")
            .OrderBy(x => x.BranchName)
            .Select(x => new SelectListItem(x.BranchName, x.BranchId.ToString()))
            .ToListAsync();

        model.ServiceOptions = await _context.Services
            .Where(x => x.Status == "Active")
            .OrderBy(x => x.ServiceName)
            .Select(x => new SelectListItem($"{x.ServiceName} - {x.Price:N0}", x.ServiceId.ToString()))
            .ToListAsync();

        if (User.IsInRole(RoleNames.Customer) && currentUserId.HasValue)
        {
            model.UserId = currentUserId.Value;
            model.UserOptions = await _context.Users
                .Where(x => x.UserId == currentUserId.Value)
                .Select(x => new SelectListItem(x.FullName, x.UserId.ToString()))
                .ToListAsync();

            model.PetOptions = await _context.Pets
                .Where(x => x.OwnerId == currentUserId.Value)
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem(x.Name, x.PetId.ToString()))
                .ToListAsync();
        }
        else
        {
            model.UserOptions = await _context.Users
                .Where(x => x.Status == "Active")
                .OrderBy(x => x.FullName)
                .Select(x => new SelectListItem($"{x.FullName} ({x.Username})", x.UserId.ToString()))
                .ToListAsync();

            model.PetOptions = await _context.Pets
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem($"{x.Name} - {x.Species}", x.PetId.ToString()))
                .ToListAsync();
        }

        return model;
    }
}

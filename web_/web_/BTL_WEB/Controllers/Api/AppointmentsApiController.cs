using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Services;
using BTL_WEB.ViewModels.Api;
using BTL_WEB.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers.Api;

[ApiController]
[Route("api/appointments")]
[Authorize(Policy = RoleNames.AllRoles)]
public class AppointmentsApiController : ControllerBase
{
    private readonly PetCareHubContext _context;
    private readonly IAppointmentWorkflowService _workflowService;

    public AppointmentsApiController(PetCareHubContext context, IAppointmentWorkflowService workflowService)
    {
        _context = context;
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? status, int page = 1, int pageSize = 10)
    {
        var query = _context.Appointments
            .Include(x => x.User)
            .Include(x => x.Pet)
            .Include(x => x.Branch)
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

        var paged = await PaginatedList<Models.Appointment>.CreateAsync(query.OrderByDescending(x => x.AppointmentDateTime), page, pageSize);
        return Ok(new
        {
            paged.PageIndex,
            paged.PageSize,
            paged.TotalItems,
            paged.TotalPages,
            items = paged.Items.Select(x => new
            {
                x.AppointmentId,
                x.AppointmentDateTime,
                x.Status,
                User = x.User.FullName,
                Pet = x.Pet.Name,
                Branch = x.Branch.BranchName
            })
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var appointment = await _context.Appointments
            .Include(x => x.User)
            .Include(x => x.Pet)
            .Include(x => x.Branch)
            .Include(x => x.AppointmentServices)
                .ThenInclude(x => x.Service)
            .FirstOrDefaultAsync(x => x.AppointmentId == id);

        if (appointment is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            appointment.AppointmentId,
            appointment.AppointmentDateTime,
            appointment.Status,
            appointment.Notes,
            User = appointment.User.FullName,
            Pet = appointment.Pet.Name,
            Branch = appointment.Branch.BranchName,
            Services = appointment.AppointmentServices.Select(x => new
            {
                x.ServiceId,
                x.Service.ServiceName,
                x.Quantity,
                x.UnitPrice
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(AppointmentApiRequest request)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var model = new AppointmentCreateViewModel
        {
            UserId = request.UserId,
            PetId = request.PetId,
            BranchId = request.BranchId,
            AppointmentDateTime = request.AppointmentDateTime,
            Notes = request.Notes,
            SelectedServiceIds = request.SelectedServiceIds
        };

        var result = await _workflowService.CreateAppointmentAsync(currentUserId.Value, User.IsInRole(RoleNames.Customer), model);
        if (!result.Success)
        {
            return BadRequest(new { result.ErrorMessage });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.AppointmentId }, new { result.AppointmentId });
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    public async Task<IActionResult> UpdateStatus(int id, AppointmentStatusApiRequest request)
    {
        var result = await _workflowService.UpdateStatusAsync(id, request.Status);
        if (!result.Success)
        {
            return BadRequest(new { result.ErrorMessage });
        }

        return Ok(new { success = true, request.Status });
    }
}

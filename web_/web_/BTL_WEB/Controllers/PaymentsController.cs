using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.ViewModels.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.AllRoles)]
public class PaymentsController : Controller
{
    private readonly PetCareHubContext _context;

    public PaymentsController(PetCareHubContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var query = _context.Payments
            .Include(x => x.Appointment)
                .ThenInclude(x => x!.User)
            .Include(x => x.Contract)
                .ThenInclude(x => x!.User)
            .AsQueryable();

        var currentUserId = User.GetUserId();
        if (User.IsInRole(RoleNames.Customer) && currentUserId.HasValue)
        {
            query = query.Where(x =>
                (x.Appointment != null && x.Appointment.UserId == currentUserId.Value) ||
                (x.Contract != null && x.Contract.UserId == currentUserId.Value));
        }

        return View(await query.OrderByDescending(x => x.PaymentDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var payment = await _context.Payments
            .Include(x => x.Appointment)
                .ThenInclude(x => x!.User)
            .Include(x => x.Appointment)
                .ThenInclude(x => x!.Pet)
            .Include(x => x.Contract)
                .ThenInclude(x => x!.User)
            .Include(x => x.Contract)
                .ThenInclude(x => x!.Pet)
            .FirstOrDefaultAsync(x => x.PaymentId == id);

        if (payment is null)
        {
            return NotFound();
        }

        var currentUserId = User.GetUserId();
        if (User.IsInRole(RoleNames.Customer) &&
            payment.Appointment?.UserId != currentUserId &&
            payment.Contract?.UserId != currentUserId)
        {
            return Forbid();
        }

        return View(payment);
    }

    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    public async Task<IActionResult> Create(int? appointmentId = null, int? contractId = null)
    {
        var model = new CreatePaymentViewModel
        {
            AppointmentId = appointmentId,
            ContractId = contractId
        };

        if (appointmentId.HasValue)
        {
            model.Amount = await _context.AppointmentServices
                .Where(x => x.AppointmentId == appointmentId.Value)
                .Select(x => x.UnitPrice * x.Quantity)
                .SumAsync();
        }
        else if (contractId.HasValue)
        {
            model.Amount = await _context.AdoptionContracts
                .Where(x => x.ContractId == contractId.Value)
                .Select(x => x.AdoptionFee)
                .FirstOrDefaultAsync();
        }

        return View(await BuildModelAsync(model));
    }

    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePaymentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        if (model.AppointmentId.HasValue)
        {
            var appointment = await _context.Appointments.FindAsync(model.AppointmentId.Value);
            if (appointment is null || appointment.Status != "Completed")
            {
                ModelState.AddModelError(nameof(model.AppointmentId), "Chi duoc tao payment cho appointment da Completed.");
            }
        }

        if (model.ContractId.HasValue)
        {
            var contractExists = await _context.AdoptionContracts.AnyAsync(x => x.ContractId == model.ContractId.Value);
            if (!contractExists)
            {
                ModelState.AddModelError(nameof(model.ContractId), "Hop dong nhan nuoi khong hop le.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(await BuildModelAsync(model));
        }

        _context.Payments.Add(new Payment
        {
            AppointmentId = model.AppointmentId,
            ContractId = model.ContractId,
            Amount = model.Amount,
            PaymentMethod = model.PaymentMethod,
            PaymentStatus = model.PaymentStatus,
            TransactionCode = model.TransactionCode,
            PaymentDate = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<CreatePaymentViewModel> BuildModelAsync(CreatePaymentViewModel model)
    {
        model.AppointmentOptions = await _context.Appointments
            .Include(x => x.User)
            .Where(x => x.Status == "Completed")
            .OrderByDescending(x => x.AppointmentDateTime)
            .Select(x => new SelectListItem($"#{x.AppointmentId} - {x.User.FullName}", x.AppointmentId.ToString()))
            .ToListAsync();

        model.ContractOptions = await _context.AdoptionContracts
            .Include(x => x.User)
            .OrderByDescending(x => x.SignedDate)
            .Select(x => new SelectListItem($"#{x.ContractId} - {x.User.FullName}", x.ContractId.ToString()))
            .ToListAsync();

        return model;
    }
}

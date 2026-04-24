using BTL_WEB.Models;
using BTL_WEB.ViewModels.Appointments;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Services;

public class AppointmentWorkflowService : IAppointmentWorkflowService
{
    private static readonly string[] AllowedStatuses = ["Pending", "Confirmed", "Completed", "Cancelled"];
    private readonly PetCareHubContext _context;

    public AppointmentWorkflowService(PetCareHubContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string? ErrorMessage, int? AppointmentId)> CreateAppointmentAsync(int currentUserId, bool isCustomer, AppointmentCreateViewModel model)
    {
        if (model.AppointmentDateTime <= DateTime.Now)
        {
            return (false, "Ngay hen phai lon hon thoi diem hien tai.", null);
        }

        if (model.SelectedServiceIds.Count == 0)
        {
            return (false, "Vui long chon it nhat mot dich vu.", null);
        }

        var userId = isCustomer ? currentUserId : model.UserId;
        var branchExists = await _context.Branches.AnyAsync(x => x.BranchId == model.BranchId && x.Status == "Active");
        if (!branchExists)
        {
            return (false, "Chi nhanh khong hop le.", null);
        }

        var petQuery = _context.Pets.Where(x => x.PetId == model.PetId);
        if (isCustomer)
        {
            petQuery = petQuery.Where(x => x.OwnerId == currentUserId);
        }

        var pet = await petQuery.FirstOrDefaultAsync();
        if (pet is null)
        {
            return (false, "Thu cung khong ton tai hoac khong thuoc nguoi dung hien tai.", null);
        }

        var userExists = await _context.Users.AnyAsync(x => x.UserId == userId && x.Status == "Active");
        if (!userExists)
        {
            return (false, "Nguoi dung khong hop le.", null);
        }

        var serviceIds = model.SelectedServiceIds.Distinct().ToList();
        var services = await _context.Services
            .Where(x => serviceIds.Contains(x.ServiceId) && x.Status == "Active")
            .ToListAsync();

        if (services.Count != serviceIds.Count)
        {
            return (false, "Danh sach dich vu khong hop le.", null);
        }

        var appointment = new Appointment
        {
            UserId = userId,
            PetId = model.PetId,
            BranchId = model.BranchId,
            AppointmentDateTime = model.AppointmentDateTime,
            Notes = model.Notes,
            Status = "Pending",
            CreatedAt = DateTime.Now
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        var appointmentServices = services.Select(service => new Models.AppointmentService
        {
            AppointmentId = appointment.AppointmentId,
            ServiceId = service.ServiceId,
            Quantity = 1,
            UnitPrice = service.Price
        }).ToList();

        _context.AppointmentServices.AddRange(appointmentServices);
        await _context.SaveChangesAsync();

        return (true, null, appointment.AppointmentId);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateStatusAsync(int appointmentId, string status)
    {
        if (!AllowedStatuses.Contains(status))
        {
            return (false, "Trang thai appointment khong hop le.");
        }

        var appointment = await _context.Appointments.FindAsync(appointmentId);
        if (appointment is null)
        {
            return (false, "Appointment khong ton tai.");
        }

        appointment.Status = status;
        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<decimal> CalculateTotalAsync(int appointmentId)
    {
        return await _context.AppointmentServices
            .Where(x => x.AppointmentId == appointmentId)
            .Select(x => x.UnitPrice * x.Quantity)
            .SumAsync();
    }
}

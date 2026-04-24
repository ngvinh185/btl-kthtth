namespace BTL_WEB.Services;

public interface IAppointmentWorkflowService
{
    Task<(bool Success, string? ErrorMessage, int? AppointmentId)> CreateAppointmentAsync(int currentUserId, bool isCustomer, BTL_WEB.ViewModels.Appointments.AppointmentCreateViewModel model);
    Task<(bool Success, string? ErrorMessage)> UpdateStatusAsync(int appointmentId, string status);
    Task<decimal> CalculateTotalAsync(int appointmentId);
}

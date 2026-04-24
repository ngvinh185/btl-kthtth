using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BTL_WEB.ViewModels.Payments;

public class CreatePaymentViewModel : IValidatableObject
{
    public int? PaymentId { get; set; }

    public int? AppointmentId { get; set; }

    public int? ContractId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(30)]
    public string PaymentMethod { get; set; } = "Cash";

    [Required]
    [StringLength(20)]
    public string PaymentStatus { get; set; } = "Paid";

    [StringLength(100)]
    public string? TransactionCode { get; set; }

    public List<SelectListItem> AppointmentOptions { get; set; } = new();

    public List<SelectListItem> ContractOptions { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var selectedCount = 0;
        if (AppointmentId.HasValue)
        {
            selectedCount++;
        }

        if (ContractId.HasValue)
        {
            selectedCount++;
        }

        if (selectedCount != 1)
        {
            yield return new ValidationResult("Payment chi duoc gan voi mot loai tham chieu tai mot thoi diem.", [nameof(AppointmentId), nameof(ContractId)]);
        }
    }
}

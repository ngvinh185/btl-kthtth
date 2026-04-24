 using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace BTL_WEB.Controllers;

[Authorize]
public class ManagementController : Controller
{
    private readonly PetCareHubContext _context;
    private readonly IConfiguration _configuration;

    public ManagementController(PetCareHubContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Pets(string? searchTerm, string? species, string? breed, int? branchId, string? status)
    {
        const string customerProvidedHealthStatus = "Khách tự cung cấp";

        var petsQuery = _context.Pets
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.Owner)
            .Where(p => p.HealthStatus != customerProvidedHealthStatus)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var keyword = searchTerm.Trim();
            var searchPattern = $"%{keyword}%";

            petsQuery = petsQuery.Where(p =>
                EF.Functions.Like(EF.Functions.Collate(p.Name, "SQL_Latin1_General_CP1_CI_AI"), searchPattern) ||
                EF.Functions.Like(EF.Functions.Collate(p.Species, "SQL_Latin1_General_CP1_CI_AI"), searchPattern) ||
                (p.Breed != null && EF.Functions.Like(EF.Functions.Collate(p.Breed, "SQL_Latin1_General_CP1_CI_AI"), searchPattern)));
        }

        if (!string.IsNullOrWhiteSpace(species))
        {
            petsQuery = petsQuery.Where(p => p.Species == species);
        }

        if (!string.IsNullOrWhiteSpace(breed))
        {
            petsQuery = petsQuery.Where(p => p.Breed == breed);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            petsQuery = petsQuery.Where(p => p.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            petsQuery = petsQuery.Where(p => p.Status == status || p.AdoptionStatus == status);
        }
        else
        {
            petsQuery = petsQuery.Where(p => p.Status == "Active");
        }

        var pets = await petsQuery
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PetSummaryViewModel
            {
                PetId = p.PetId,
                Name = p.Name,
                Species = p.Species,
                Breed = p.Breed,
                Gender = p.Gender,
                VaccinationStatus = p.VaccinationStatus,
                AdoptionStatus = p.AdoptionStatus,
                Status = p.Status,
                BranchId = p.BranchId,
                BranchName = p.Branch.BranchName,
                OwnerName = p.Owner != null ? p.Owner.FullName : null
            })
            .ToListAsync();

        var petIds = pets.Select(p => p.PetId).ToList();

        var images = await _context.PetImages
            .AsNoTracking()
            .Where(i => petIds.Contains(i.PetId))
            .OrderByDescending(i => i.UploadedAt)
            .Take(60)
            .Select(i => new PetImageSummaryViewModel
            {
                ImageId = i.ImageId,
                PetId = i.PetId,
                PetName = i.Pet.Name,
                ImageUrl = i.ImageUrl,
                IsPrimary = i.IsPrimary,
                UploadedAt = i.UploadedAt
            })
            .ToListAsync();

        var vaccinations = await _context.Vaccinations
            .AsNoTracking()
            .Where(v => petIds.Contains(v.PetId))
            .OrderByDescending(v => v.VaccinationDate)
            .Take(60)
            .Select(v => new VaccinationSummaryViewModel
            {
                VaccinationId = v.VaccinationId,
                PetId = v.PetId,
                PetName = v.Pet.Name,
                VaccineName = v.VaccineName,
                VaccinationDate = v.VaccinationDate,
                NextDueDate = v.NextDueDate,
                StaffName = v.Staff.User.FullName
            })
            .ToListAsync();

        var medicalRecords = await _context.MedicalRecords
            .AsNoTracking()
            .Where(m => petIds.Contains(m.PetId))
            .OrderByDescending(m => m.VisitDate)
            .Take(60)
            .Select(m => new MedicalRecordSummaryViewModel
            {
                RecordId = m.RecordId,
                PetId = m.PetId,
                PetName = m.Pet.Name,
                VisitDate = m.VisitDate,
                Diagnosis = m.Diagnosis,
                Treatment = m.Treatment,
                StaffName = m.Staff.User.FullName
            })
            .ToListAsync();

        ViewBag.SpeciesOptions = await _context.Pets
            .AsNoTracking()
            .Where(p => p.HealthStatus != customerProvidedHealthStatus)
            .Select(p => p.Species)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync();

        ViewBag.BreedOptions = await _context.Pets
            .AsNoTracking()
            .Where(p => p.HealthStatus != customerProvidedHealthStatus)
            .Where(p => p.Breed != null && p.Breed != string.Empty)
            .Select(p => p.Breed!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync();

        ViewBag.BranchOptions = await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.BranchName)
            .Select(b => new { b.BranchId, b.BranchName })
            .ToListAsync();

        var model = new PetsPageViewModel
        {
            SearchTerm = searchTerm,
            Species = species,
            Breed = breed,
            BranchId = branchId,
            Status = status,
            Pets = pets,
            Images = images,
            Vaccinations = vaccinations,
            MedicalRecords = medicalRecords
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentStatus(int appointmentId, string status, string? returnUrl)
    {
        if (appointmentId <= 0 || string.IsNullOrWhiteSpace(status))
        {
            TempData["ErrorMessage"] = "Dữ liệu cập nhật trạng thái không hợp lệ.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        var targetStatus = NormalizeAppointmentStatus(status);
        if (targetStatus is null)
        {
            TempData["ErrorMessage"] = "Trạng thái lịch hẹn không hợp lệ.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);
        if (appointment is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn cần cập nhật.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        var currentStatus = NormalizeAppointmentStatus(appointment.Status);
        if (currentStatus is null)
        {
            TempData["ErrorMessage"] = "Trạng thái hiện tại của lịch hẹn không hợp lệ.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        if (currentStatus == targetStatus)
        {
            TempData["SuccessMessage"] = "Lịch hẹn đã ở trạng thái này.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        if (!CanTransitionAppointmentStatus(currentStatus, targetStatus))
        {
            TempData["ErrorMessage"] = "Không thể chuyển trạng thái lịch hẹn theo thao tác đã chọn.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        appointment.Status = targetStatus;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Cập nhật trạng thái lịch hẹn thành công.";
        return RedirectAfterUpdateAppointmentStatus(returnUrl);
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AllRoles)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMyAppointment(int appointmentId, string? returnUrl)
    {
        var userId = await ResolveCurrentUserIdAsync();
        if (!userId.HasValue)
        {
            return Challenge();
        }

        var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);
        if (appointment is null || appointment.UserId != userId.Value)
        {
            TempData["ErrorMessage"] = "Khong tim thay lich hen cua ban de huy.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        var currentStatus = NormalizeAppointmentStatus(appointment.Status);
        if (currentStatus is null)
        {
            TempData["ErrorMessage"] = "Trang thai hien tai cua lich hen khong hop le.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        if (!CanTransitionAppointmentStatus(currentStatus, "Cancelled"))
        {
            TempData["ErrorMessage"] = "Chi co the huy lich hen dang cho hoac da xac nhan.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        appointment.Status = "Cancelled";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Da huy lich hen thanh cong.";
        return RedirectAfterUpdateAppointmentStatus(returnUrl);
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentPaymentStatus(int appointmentId, string paymentStatus, string? returnUrl)
    {
        if (appointmentId <= 0)
        {
            TempData["ErrorMessage"] = "Dữ liệu cập nhật thanh toán không hợp lệ.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        var normalizedStatus = string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
            ? "Paid"
            : "Pending";

        var appointment = await _context.Appointments
            .Include(a => a.Payments)
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

        if (appointment is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn cần cập nhật thanh toán.";
            return RedirectAfterUpdateAppointmentStatus(returnUrl);
        }

        var payment = appointment.Payments
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault();

        if (payment is null)
        {
            var amount = appointment.AppointmentServices.Sum(s => s.Quantity * s.UnitPrice);
            payment = new Payment
            {
                AppointmentId = appointmentId,
                Amount = amount <= 0 ? 0 : amount,
                PaymentMethod = "Cash",
                PaymentDate = DateTime.Now,
                PaymentStatus = normalizedStatus
            };

            _context.Payments.Add(payment);
        }
        else
        {
            payment.PaymentStatus = normalizedStatus;
            payment.PaymentDate = DateTime.Now;
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = normalizedStatus == "Paid"
            ? "Đã xác nhận thanh toán cho lịch hẹn."
            : "Đã chuyển trạng thái về chưa thanh toán.";

        return RedirectAfterUpdateAppointmentStatus(returnUrl);
    }

    [AllowAnonymous]
    public async Task<IActionResult> PetDetail(int id)
    {
        var pet = await _context.Pets
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.PetImages)
            .FirstOrDefaultAsync(p => p.PetId == id);

        if (pet is null)
        {
            return NotFound();
        }

        var vaccinations = await _context.Vaccinations
            .AsNoTracking()
            .Where(v => v.PetId == id)
            .OrderByDescending(v => v.VaccinationDate)
            .Take(10)
            .Select(v => new VaccinationHistoryItemViewModel
            {
                VaccineName = v.VaccineName,
                VaccinationDate = v.VaccinationDate,
                Notes = v.Notes
            })
            .ToListAsync();

        var model = new PetDetailViewModel
        {
            PetId = pet.PetId,
            Name = pet.Name,
            Species = pet.Species,
            Breed = pet.Breed,
            Gender = pet.Gender,
            MicrochipId = $"MC-{pet.PetId:D6}",
            HealthStatus = pet.HealthStatus,
            Weight = pet.Weight,
            AdoptionStatus = pet.AdoptionStatus,
            Status = pet.Status,
            BranchName = pet.Branch.BranchName,
            BranchAddress = pet.Branch.Address,
            BranchHotline = pet.Branch.Phone,
            Description = pet.Description,
            ImageUrls = pet.PetImages
                .OrderByDescending(i => i.IsPrimary)
                .ThenByDescending(i => i.UploadedAt)
                .Select(i => i.ImageUrl)
                .ToList(),
            Vaccinations = vaccinations
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAppointment(int? petId, int branchId, DateTime appointmentDateTime, int? selectedServiceId, string? notes, string? petSpecies, string? petBreed, string? petGender, string? returnUrl)
    {
        var userId = await ResolveCurrentUserIdAsync();
        var isStaffOrAdmin = IsStaffOrAdmin();
        if (!userId.HasValue)
        {
            TempData["ErrorMessage"] = "Không xác định được người dùng hiện tại.";
            return RedirectAfterCreateAppointment(returnUrl);
        }

        var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
        if (!branchExists || appointmentDateTime <= DateTime.Now)
        {
            TempData["ErrorMessage"] = "Thông tin lịch hẹn không hợp lệ.";
            return RedirectAfterCreateAppointment(returnUrl);
        }

        var selectedPetId = petId;
        if (!selectedPetId.HasValue || selectedPetId.Value <= 0)
        {
            if (!isStaffOrAdmin)
            {
                var generatedPet = new Pet
                {
                    Name = $"Thú cưng của {userId.Value}",
                    Species = string.IsNullOrWhiteSpace(petSpecies) ? "Khác" : petSpecies.Trim(),
                    Breed = string.IsNullOrWhiteSpace(petBreed) ? null : petBreed.Trim(),
                    Gender = string.IsNullOrWhiteSpace(petGender) ? null : petGender.Trim(),
                    HealthStatus = "Khách tự cung cấp",
                    VaccinationStatus = "Chưa cập nhật",
                    AdoptionStatus = "Adopted",
                    BranchId = branchId,
                    OwnerId = userId.Value,
                    CreatedAt = DateTime.Now,
                    Status = "Active"
                };

                _context.Pets.Add(generatedPet);
                await _context.SaveChangesAsync();
                selectedPetId = generatedPet.PetId;
            }
            else
            {
                var fallbackPetQuery = _context.Pets
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(petSpecies))
                {
                    var species = petSpecies.Trim();
                    fallbackPetQuery = fallbackPetQuery.Where(p => p.Species == species);
                }

                if (!string.IsNullOrWhiteSpace(petBreed))
                {
                    var breed = petBreed.Trim();
                    fallbackPetQuery = fallbackPetQuery.Where(p => p.Breed == breed);
                }

                if (!string.IsNullOrWhiteSpace(petGender))
                {
                    var gender = petGender.Trim();
                    fallbackPetQuery = fallbackPetQuery.Where(p => p.Gender == gender);
                }

                selectedPetId = await fallbackPetQuery
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => (int?)p.PetId)
                    .FirstOrDefaultAsync();
            }
        }

        if (!selectedPetId.HasValue || selectedPetId.Value <= 0)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thú cưng phù hợp với thông tin đã chọn.";
            return RedirectAfterCreateAppointment(returnUrl);
        }

        var petExistsQuery = _context.Pets
            .AsNoTracking()
            .Where(p => p.PetId == selectedPetId.Value);

        if (!isStaffOrAdmin)
        {
            petExistsQuery = petExistsQuery.Where(p =>
                p.OwnerId == userId.Value ||
                p.AdoptionContracts.Any(c => c.UserId == userId.Value && c.Status != "Cancelled"));
        }

        var petExists = await petExistsQuery.AnyAsync();
        if (!petExists)
        {
            TempData["ErrorMessage"] = "Thông tin lịch hẹn không hợp lệ.";
            return RedirectAfterCreateAppointment(returnUrl);
        }

        var appointment = new Appointment
        {
            UserId = userId.Value,
            PetId = selectedPetId.Value,
            BranchId = branchId,
            AppointmentDateTime = appointmentDateTime,
            Status = "Pending",
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.Now
        };

        _context.Appointments.Add(appointment);
        await _context.SaveChangesAsync();

        if (selectedServiceId.HasValue && selectedServiceId.Value > 0)
        {
            var service = await _context.Services
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ServiceId == selectedServiceId.Value && s.Status == "Active");

            if (service is not null)
            {
                _context.AppointmentServices.Add(new AppointmentService
                {
                    AppointmentId = appointment.AppointmentId,
                    ServiceId = service.ServiceId,
                    Quantity = 1,
                    UnitPrice = service.Price
                });
                await _context.SaveChangesAsync();
            }
        }

        TempData["SuccessMessage"] = "Đã tạo lịch hẹn thành công.";
        return RedirectAfterCreateAppointment(returnUrl);
    }

    public async Task<IActionResult> Appointments(string? status, DateTime? date, int? selectedServiceId, int? selectedPetId, string? sortBy = "CreatedAt", string? sortDirection = "desc")
    {
        var currentUserId = await ResolveCurrentUserIdAsync();
        var isStaffOrAdmin = IsStaffOrAdmin();

        if (!string.IsNullOrWhiteSpace(sortBy) && sortBy.Contains('|'))
        {
            var parts = sortBy.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                sortBy = parts[0];
                sortDirection = parts[1];
            }
        }

        var normalizedSortBy = "AppointmentDateTime";
        var normalizedSortDirection = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";

        var appointmentsQuery = _context.Appointments
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.Pet)
            .Include(a => a.Branch)
            .AsQueryable();

        if (!isStaffOrAdmin)
        {
            if (!currentUserId.HasValue)
            {
                return Challenge();
            }

            appointmentsQuery = appointmentsQuery.Where(a => a.UserId == currentUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            appointmentsQuery = appointmentsQuery.Where(a => a.Status == status);
        }

        if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            appointmentsQuery = appointmentsQuery.Where(a => a.AppointmentDateTime.Date == targetDate);
        }

        var sortedAppointmentsQuery = (normalizedSortBy, normalizedSortDirection) switch
        {
            ("AppointmentDateTime", "asc") => appointmentsQuery.OrderBy(a => a.AppointmentDateTime).ThenBy(a => a.CreatedAt),
            ("AppointmentDateTime", _) => appointmentsQuery.OrderByDescending(a => a.AppointmentDateTime).ThenByDescending(a => a.CreatedAt),
            ("CreatedAt", "asc") => appointmentsQuery.OrderBy(a => a.CreatedAt).ThenBy(a => a.AppointmentDateTime),
            _ => appointmentsQuery.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AppointmentDateTime)
        };

        var appointments = await sortedAppointmentsQuery
            .Take(50)
            .Select(a => new AppointmentSummaryViewModel
            {
                AppointmentId = a.AppointmentId,
                UserId = a.UserId,
                UserName = a.User.FullName,
                PetId = a.PetId,
                PetName = a.Pet.Name,
                BranchId = a.BranchId,
                BranchName = a.Branch.BranchName,
                AppointmentDateTime = a.AppointmentDateTime,
                Status = a.Status,
                Notes = a.Notes,
                PaymentAmount = a.AppointmentServices
                    .Select(s => (decimal?)(s.Quantity * s.UnitPrice))
                    .Sum() ?? 0m,
                PaymentStatus = a.Payments
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p => p.PaymentStatus)
                    .FirstOrDefault() ?? "Pending"
            })
            .ToListAsync();

        foreach (var appointment in appointments)
        {
            appointment.Notes = NormalizeDisplayNote(appointment.Notes);
            appointment.PaymentAmountDisplay = $"{appointment.PaymentAmount:N0} đ";
            appointment.PaymentTransferContent = BuildAppointmentTransferContent(appointment.AppointmentId);
            appointment.PaymentBankName = GetPaymentQrSetting("BankName");
            appointment.PaymentAccountNumber = GetPaymentQrSetting("AccountNumber");
            appointment.PaymentAccountName = GetPaymentQrSetting("AccountName");
            appointment.PaymentQrImageUrl = BuildAppointmentPaymentQrUrl(appointment.AppointmentId, appointment.PaymentAmount);
        }

        ViewBag.IsStaffOrAdmin = isStaffOrAdmin;

        ViewBag.UserOptions = await _context.Users
            .AsNoTracking()
            .Where(u => isStaffOrAdmin || (currentUserId.HasValue && u.UserId == currentUserId.Value))
            .OrderBy(u => u.FullName)
            .Take(100)
            .Select(u => new { u.UserId, u.FullName })
            .ToListAsync();

        var petOptionsQuery = _context.Pets
            .AsNoTracking()
            .AsQueryable();

        if (!isStaffOrAdmin)
        {
            petOptionsQuery = petOptionsQuery.Where(p =>
                currentUserId.HasValue &&
                (p.OwnerId == currentUserId.Value || p.AdoptionContracts.Any(c => c.UserId == currentUserId.Value && c.Status != "Cancelled")));
        }

        ViewBag.PetOptions = await petOptionsQuery
            .OrderBy(p => p.Name)
            .Take(100)
            .Select(p => new
            {
                p.PetId,
                p.Name,
                p.Species,
                p.Breed,
                p.Gender
            })
            .ToListAsync();

        ViewBag.BranchOptions = await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.BranchName)
            .Select(b => new { b.BranchId, b.BranchName })
            .ToListAsync();

        ViewBag.ServiceOptions = await _context.Services
            .AsNoTracking()
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.ServiceName)
            .Select(s => new { s.ServiceId, s.ServiceName })
            .ToListAsync();

        ViewBag.PetSpeciesOptions = await _context.Pets
            .AsNoTracking()
            .Where(p => p.Species != null && p.Species != string.Empty)
            .Select(p => p.Species)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        ViewBag.PetBreedOptions = await _context.Pets
            .AsNoTracking()
            .Where(p => p.Breed != null && p.Breed != string.Empty)
            .Select(p => p.Breed)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var appointmentIds = appointments.Select(a => a.AppointmentId).ToList();

        var appointmentServices = await _context.AppointmentServices
            .AsNoTracking()
            .Where(s => appointmentIds.Contains(s.AppointmentId))
            .OrderByDescending(s => s.AppointmentId)
            .Take(100)
            .Select(s => new AppointmentServiceSummaryViewModel
            {
                AppointmentId = s.AppointmentId,
                ServiceId = s.ServiceId,
                ServiceName = s.Service.ServiceName,
                Quantity = s.Quantity,
                UnitPrice = s.UnitPrice
            })
            .ToListAsync();

        var model = new AppointmentsPageViewModel
        {
            Status = status,
            Date = date,
            SelectedServiceId = selectedServiceId,
            SelectedPetId = selectedPetId,
            SortBy = normalizedSortBy,
            SortDirection = normalizedSortDirection,
            Appointments = appointments,
            AppointmentServices = appointmentServices
        };

        return View(model);
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AllRoles)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdoptionRequest(int petId, string message)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return AdoptionRequestUnauthorized();
        }

        var userId = await ResolveCurrentUserIdAsync();
        if (IsStaffOrAdmin())
        {
            return AdoptionRequestResponse(false, "Tài khoản quản trị không dùng để gửi yêu cầu nhận nuôi.");
        }

        if (!userId.HasValue)
        {
            return AdoptionRequestResponse(false, "Không xác định được người dùng hiện tại.");
        }

        var pet = await _context.Pets.FirstOrDefaultAsync(p => p.PetId == petId);

        if (pet is null || !string.Equals(pet.AdoptionStatus, "Available", StringComparison.OrdinalIgnoreCase))
        {
            return AdoptionRequestResponse(false, "Không thể tạo yêu cầu nhận nuôi với dữ liệu đã chọn.");
        }

        var hasPendingRequest = await _context.AdoptionRequests
            .AnyAsync(r => r.PetId == petId && r.Status == "Pending");

        if (hasPendingRequest)
        {
            pet.AdoptionStatus = "Pending";
            await _context.SaveChangesAsync();
            return AdoptionRequestResponse(false, "Thú cưng này đã có yêu cầu nhận nuôi đang chờ xử lý.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return AdoptionRequestResponse(false, "Vui lòng nhập nội dung yêu cầu nhận nuôi.");
        }

        var request = new AdoptionRequest
        {
            UserId = userId.Value,
            PetId = petId,
            RequestDate = DateTime.Now,
            Status = "Pending",
            Message = message.Trim()
        };

        _context.AdoptionRequests.Add(request);
        pet.AdoptionStatus = "Pending";
        await _context.SaveChangesAsync();

        return AdoptionRequestResponse(true, "Đã gửi yêu cầu nhận nuôi thành công.");
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.StaffOrAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAdoptionRequestStatus(int requestId, string status, string? returnUrl)
    {
        if (requestId <= 0 || string.IsNullOrWhiteSpace(status))
        {
            TempData["ErrorMessage"] = "Dữ liệu duyệt yêu cầu nhận nuôi không hợp lệ.";
            return RedirectAfterUpdateAdoptionRequestStatus(returnUrl);
        }

        var targetStatus = status.Trim();
        if (!string.Equals(targetStatus, "Approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Trạng thái duyệt yêu cầu nhận nuôi không hợp lệ.";
            return RedirectAfterUpdateAdoptionRequestStatus(returnUrl);
        }

        var request = await _context.AdoptionRequests
            .Include(r => r.Pet)
            .Include(r => r.AdoptionContract)
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (request is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy yêu cầu nhận nuôi cần xử lý.";
            return RedirectAfterUpdateAdoptionRequestStatus(returnUrl);
        }

        if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Yêu cầu này đã được xử lý trước đó.";
            return RedirectAfterUpdateAdoptionRequestStatus(returnUrl);
        }

        var staffIdClaim = User.FindFirstValue("StaffId");
        request.ReviewedByStaffId = int.TryParse(staffIdClaim, out var staffId) ? staffId : null;
        request.ReviewedAt = DateTime.Now;

        if (string.Equals(targetStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            request.Status = "Approved";
            request.Pet.AdoptionStatus = "Adopted";
            request.Pet.OwnerId = request.UserId;

            if (request.AdoptionContract is null)
            {
                _context.AdoptionContracts.Add(new AdoptionContract
                {
                    RequestId = request.RequestId,
                    PetId = request.PetId,
                    UserId = request.UserId,
                    SignedDate = DateTime.Now,
                    AdoptionFee = 0,
                    Status = "Active"
                });
            }

            var otherPendingRequests = await _context.AdoptionRequests
                .Where(r => r.PetId == request.PetId && r.RequestId != request.RequestId && r.Status == "Pending")
                .ToListAsync();

            foreach (var pending in otherPendingRequests)
            {
                pending.Status = "Rejected";
                pending.ReviewedAt = DateTime.Now;
                pending.ReviewedByStaffId = request.ReviewedByStaffId;
            }

            TempData["SuccessMessage"] = "Đã duyệt yêu cầu nhận nuôi thành công.";
        }
        else
        {
            request.Status = "Rejected";

            var hasOtherPending = await _context.AdoptionRequests
                .AnyAsync(r => r.PetId == request.PetId && r.RequestId != request.RequestId && r.Status == "Pending");

            request.Pet.AdoptionStatus = hasOtherPending ? "Pending" : "Available";
            TempData["SuccessMessage"] = "Đã từ chối yêu cầu nhận nuôi.";
        }

        await _context.SaveChangesAsync();
        return RedirectAfterUpdateAdoptionRequestStatus(returnUrl);
    }

    public async Task<IActionResult> Adoptions(string? status, int petPage = 1)
    {
        var currentUserId = await ResolveCurrentUserIdAsync();
        var isStaffOrAdmin = IsStaffOrAdmin();
        const int petPageSize = 8;

        if (petPage < 1)
        {
            petPage = 1;
        }

        var requestsQuery = _context.AdoptionRequests
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Pet)
            .Include(r => r.ReviewedByStaff)
                .ThenInclude(s => s!.User)
            .AsQueryable();

        if (!isStaffOrAdmin)
        {
            if (!currentUserId.HasValue)
            {
                return Challenge();
            }

            requestsQuery = requestsQuery.Where(r => r.UserId == currentUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            requestsQuery = requestsQuery.Where(r => r.Status == status);
        }

        var requests = await requestsQuery
            .OrderByDescending(r => r.RequestDate)
            .Take(50)
            .Select(r => new AdoptionRequestSummaryViewModel
            {
                RequestId = r.RequestId,
                UserId = r.UserId,
                UserName = r.User.FullName,
                PetId = r.PetId,
                PetName = r.Pet.Name,
                RequestDate = r.RequestDate,
                Status = r.Status,
                ReviewedBy = r.ReviewedByStaff != null ? r.ReviewedByStaff.User.FullName : null
            })
            .ToListAsync();

        var contractsQuery = _context.AdoptionContracts
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Pet)
            .AsQueryable();

        if (!isStaffOrAdmin)
        {
            contractsQuery = contractsQuery.Where(c => currentUserId.HasValue && c.UserId == currentUserId.Value);
        }

        var contracts = await contractsQuery
            .OrderByDescending(c => c.SignedDate)
            .Take(50)
            .Select(c => new AdoptionContractSummaryViewModel
            {
                ContractId = c.ContractId,
                RequestId = c.RequestId,
                UserId = c.UserId,
                UserName = c.User.FullName,
                PetId = c.PetId,
                PetName = c.Pet.Name,
                AdoptionFee = c.AdoptionFee,
                Status = c.Status
            })
            .ToListAsync();

        var paymentsQuery = _context.Payments
            .AsNoTracking()
            .Where(p => p.ContractId != null)
            .AsQueryable();

        if (!isStaffOrAdmin)
        {
            paymentsQuery = paymentsQuery.Where(p => currentUserId.HasValue && p.Contract != null && p.Contract.UserId == currentUserId.Value);
        }

        var payments = await paymentsQuery
            .OrderByDescending(p => p.PaymentDate)
            .Take(50)
            .Select(p => new PaymentSummaryViewModel
            {
                PaymentId = p.PaymentId,
                AppointmentId = p.AppointmentId,
                ContractId = p.ContractId,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                PaymentDate = p.PaymentDate,
                PaymentStatus = p.PaymentStatus
            })
            .ToListAsync();

        var availablePetsQuery = _context.Pets
            .AsNoTracking()
            .Include(p => p.Branch)
            .Include(p => p.PetImages)
            .Where(p => p.AdoptionStatus == "Available")
            .OrderByDescending(p => p.CreatedAt);

        var availablePetsTotalItems = await availablePetsQuery.CountAsync();
        var availablePetsTotalPages = Math.Max(1, (int)Math.Ceiling(availablePetsTotalItems / (double)petPageSize));
        if (petPage > availablePetsTotalPages)
        {
            petPage = availablePetsTotalPages;
        }

        var availablePets = await availablePetsQuery
            .Skip((petPage - 1) * petPageSize)
            .Take(petPageSize)
            .Select(p => new AvailablePetCardViewModel
            {
                PetId = p.PetId,
                Name = p.Name,
                Species = p.Species,
                Breed = p.Breed,
                Gender = p.Gender,
                AdoptionStatus = p.AdoptionStatus,
                BranchName = p.Branch.BranchName,
                PrimaryImageUrl = p.PetImages
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenByDescending(i => i.UploadedAt)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync();

        ViewBag.IsStaffOrAdmin = isStaffOrAdmin;

        var model = new AdoptionsPageViewModel
        {
            Status = status,
            AvailablePetsPageIndex = petPage,
            AvailablePetsPageSize = petPageSize,
            AvailablePetsTotalItems = availablePetsTotalItems,
            AvailablePetsTotalPages = availablePetsTotalPages,
            AvailablePets = availablePets,
            Requests = requests,
            Contracts = contracts,
            Payments = payments
        };

        return View(model);
    }

    private Task<int?> ResolveCurrentUserIdAsync()
    {
        var claimValue = User.FindFirstValue(ClaimNames.UserId) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(claimValue, out var userId))
        {
            return Task.FromResult<int?>(userId);
        }

        return Task.FromResult<int?>(null);
    }

    private bool IsStaffOrAdmin()
    {
        return User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Staff);
    }

    private IActionResult RedirectAfterCreateAppointment(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Appointments));
    }

    private IActionResult RedirectAfterUpdateAdoptionRequestStatus(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Adoptions));
    }

    private IActionResult RedirectAfterUpdateAppointmentStatus(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Appointments));
    }

    private IActionResult AdoptionRequestResponse(bool success, string message)
    {
        if (Request.IsAjaxRequest())
        {
            return StatusCode(success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest, new
            {
                success,
                message
            });
        }

        TempData[success ? "SuccessMessage" : "ErrorMessage"] = message;
        return RedirectToAction(nameof(Adoptions));
    }

    private IActionResult AdoptionRequestUnauthorized()
    {
        const string message = "Vui lòng đăng nhập bằng tài khoản khách hàng trước khi gửi yêu cầu nhận nuôi.";

        if (Request.IsAjaxRequest())
        {
            return Unauthorized(new
            {
                success = false,
                message
            });
        }

        TempData["ErrorMessage"] = message;
        return RedirectToAction("Login", "Account", new
        {
            returnUrl = Url.Action(nameof(Adoptions), "Management")
        });
    }

    private static string? NormalizeDisplayNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var normalized = note.Trim();
        if (normalized.Contains("mẫu", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("mau", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("sample", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private string? GetPaymentQrSetting(string key)
    {
        var value = _configuration[$"PaymentQr:{key}"];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string BuildAppointmentTransferContent(int appointmentId)
    {
        var transferPrefix = GetPaymentQrSetting("TransferPrefix") ?? "LICH HEN";
        return $"{transferPrefix} {appointmentId}";
    }

    private string? BuildAppointmentPaymentQrUrl(int appointmentId, decimal amount)
    {
        if (amount <= 0)
        {
            return null;
        }

        var bankId = GetPaymentQrSetting("BankId");
        var accountNumber = GetPaymentQrSetting("AccountNumber");
        var accountName = GetPaymentQrSetting("AccountName");
        var template = GetPaymentQrSetting("Template") ?? "compact2";

        if (string.IsNullOrWhiteSpace(bankId)
            || string.IsNullOrWhiteSpace(accountNumber)
            || string.IsNullOrWhiteSpace(accountName))
        {
            return null;
        }

        var transferContent = BuildAppointmentTransferContent(appointmentId);
        var encodedTransferContent = Uri.EscapeDataString(transferContent);
        var encodedAccountName = Uri.EscapeDataString(accountName);
        var roundedAmount = decimal.ToInt64(decimal.Round(amount, 0, MidpointRounding.AwayFromZero));

        return $"https://img.vietqr.io/image/{bankId}-{accountNumber}-{template}.png?amount={roundedAmount}&addInfo={encodedTransferContent}&accountName={encodedAccountName}";
    }

    private static string? NormalizeAppointmentStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "confirmed" => "Confirmed",
            "completed" => "Completed",
            "cancelled" => "Cancelled",
            _ => null
        };
    }

    private static bool CanTransitionAppointmentStatus(string currentStatus, string targetStatus)
    {
        return currentStatus switch
        {
            "Pending" => targetStatus is "Confirmed" or "Cancelled",
            "Confirmed" => targetStatus is "Completed" or "Cancelled",
            _ => false
        };
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSystemUser(int userId, string username, string fullName, string email, string status, string? keyword)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null || user.RoleId != await GetCustomerRoleIdAsync())
        {
            TempData["ErrorMessage"] = "Khong tim thay user can cap nhat.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        username = username?.Trim() ?? string.Empty;
        fullName = fullName?.Trim() ?? string.Empty;
        email = email?.Trim() ?? string.Empty;
        status = string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Thong tin user khong hop le.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        var normalizedUsername = username.ToLower();
        var normalizedEmail = email.ToLower();

        if (await _context.Users.AnyAsync(u => u.UserId != userId && u.Username.ToLower() == normalizedUsername))
        {
            TempData["ErrorMessage"] = "Username da ton tai.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        if (await _context.Users.AnyAsync(u => u.UserId != userId && u.Email.ToLower() == normalizedEmail))
        {
            TempData["ErrorMessage"] = "Email da duoc su dung.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        user.Username = username;
        user.FullName = fullName;
        user.Email = email;
        user.Status = status;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Da cap nhat user.";
        return RedirectToAction(nameof(System), new { keyword });
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSystemUser(int userId, string? keyword)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user is null || user.RoleId != await GetCustomerRoleIdAsync())
        {
            TempData["ErrorMessage"] = "Khong tim thay user can xoa.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        user.Status = "Inactive";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Da xoa mem user.";
        return RedirectToAction(nameof(System), new { keyword });
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSystemStaff(int staffId, string fullName, int branchId, string position, string status, string? keyword)
    {
        var staff = await _context.Staff
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffId == staffId);

        if (staff is null)
        {
            TempData["ErrorMessage"] = "Khong tim thay staff can cap nhat.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        fullName = fullName?.Trim() ?? string.Empty;
        position = position?.Trim() ?? string.Empty;
        status = string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(position) || branchId <= 0)
        {
            TempData["ErrorMessage"] = "Thong tin staff khong hop le.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
        if (!branchExists)
        {
            TempData["ErrorMessage"] = "Chi nhanh khong ton tai.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        staff.User.FullName = fullName;
        staff.BranchId = branchId;
        staff.Position = position;
        staff.Status = status;
        staff.User.Status = status;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Da cap nhat staff.";
        return RedirectToAction(nameof(System), new { keyword });
    }

    [HttpPost]
    [Authorize(Policy = RoleNames.AdminOnly)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSystemStaff(int staffId, string? keyword)
    {
        var staff = await _context.Staff
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StaffId == staffId);

        if (staff is null)
        {
            TempData["ErrorMessage"] = "Khong tim thay staff can xoa.";
            return RedirectToAction(nameof(System), new { keyword });
        }

        staff.Status = "Inactive";
        staff.User.Status = "Inactive";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Da xoa mem staff.";
        return RedirectToAction(nameof(System), new { keyword });
    }

    [Authorize(Policy = RoleNames.AdminOnly)]
    public async Task<IActionResult> System(string? keyword)
    {
        var usersQuery = _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .AsQueryable();

        var customerRoleId = await GetCustomerRoleIdAsync();
        usersQuery = usersQuery.Where(u => u.RoleId == customerRoleId && u.Status == "Active");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var key = keyword.Trim();
            usersQuery = usersQuery.Where(u => u.Username.Contains(key) || u.FullName.Contains(key) || u.Email.Contains(key));
        }

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Take(60)
            .Select(u => new UserSummaryViewModel
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                RoleName = u.Role.RoleName,
                Status = u.Status,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        var staff = await _context.Staff
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Branch)
            .Where(s => s.Status == "Active")
            .OrderByDescending(s => s.StaffId)
            .Take(60)
            .Select(s => new StaffSummaryViewModel
            {
                StaffId = s.StaffId,
                UserId = s.UserId,
                FullName = s.User.FullName,
                BranchId = s.BranchId,
                BranchName = s.Branch.BranchName,
                Position = s.Position,
                Status = s.Status,
                CreatedAt = s.User.CreatedAt
            })
            .ToListAsync();

        var payments = await _context.Payments
            .AsNoTracking()
            .OrderByDescending(p => p.PaymentDate)
            .Take(50)
            .Select(p => new PaymentSummaryViewModel
            {
                PaymentId = p.PaymentId,
                AppointmentId = p.AppointmentId,
                ContractId = p.ContractId,
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod,
                PaymentDate = p.PaymentDate,
                PaymentStatus = p.PaymentStatus
            })
            .ToListAsync();

        var topServices = await _context.AppointmentServices
            .AsNoTracking()
            .Include(a => a.Service)
            .GroupBy(a => new { a.ServiceId, a.Service.ServiceName })
            .Select(g => new TopServiceSummaryViewModel
            {
                ServiceId = g.Key.ServiceId,
                ServiceName = g.Key.ServiceName,
                TotalQuantity = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.TotalQuantity)
            .ThenByDescending(x => x.TotalRevenue)
            .Take(8)
            .ToListAsync();

        ViewBag.BranchOptions = await _context.Branches
            .AsNoTracking()
            .OrderBy(b => b.BranchName)
            .Select(b => new { b.BranchId, b.BranchName })
            .ToListAsync();

        var model = new SystemPageViewModel
        {
            Keyword = keyword,
            Users = users,
            Staff = staff,
            Payments = payments,
            TopServices = topServices
        };

        return View(model);
    }

    private async Task<int> GetCustomerRoleIdAsync()
    {
        var customerRole = await _context.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoleName == RoleNames.Customer);

        return customerRole?.RoleId ?? 0;
    }
}

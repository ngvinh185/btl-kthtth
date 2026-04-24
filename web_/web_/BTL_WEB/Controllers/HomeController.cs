using System.Diagnostics;
using System.Security.Claims;
using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers
{
    public class HomeController : Controller
    {
        private readonly PetCareHubContext _context;

        public HomeController(PetCareHubContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var petsCount = await _context.Pets.AsNoTracking().CountAsync();
            var branchesCount = await _context.Branches.AsNoTracking().CountAsync();
            var staffCount = await _context.Staff.AsNoTracking().CountAsync();

            var featuredServices = await _context.Services
                .AsNoTracking()
                .Where(x => x.Status == "Active")
                .OrderBy(x => x.ServiceName)
                .Take(3)
                .Select(x => new HomeFeaturedServiceViewModel
                {
                    ServiceId = x.ServiceId,
                    ServiceName = x.ServiceName,
                    Description = x.Description,
                    Price = x.Price
                })
                .ToListAsync();

            var model = new HomePageViewModel
            {
                PetsCount = petsCount,
                BranchesCount = branchesCount,
                StaffCount = staffCount,
                HappyClients = Math.Max(120, petsCount * 3),
                FeaturedServices = featuredServices,
                Testimonials = new List<HomeTestimonialViewModel>
                {
                    new HomeTestimonialViewModel { CustomerName = "Ngoc Anh", Comment = "Dich vu rat tot, thu cung duoc cham soc ky.", Rating = 5 },
                    new HomeTestimonialViewModel { CustomerName = "Minh Khang", Comment = "Dat lich nhanh va nhan vien tu van nhiet tinh.", Rating = 5 },
                    new HomeTestimonialViewModel { CustomerName = "Bao Tran", Comment = "Khong gian sach se, be nha minh rat thich.", Rating = 4 }
                }
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> Dashboard()
        {
            var currentUser = await ResolveCurrentUserAsync();
            if (currentUser is null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Staff))
            {
                return RedirectToAction("System", "Management");
            }

            var myPets = await _context.Pets
                .AsNoTracking()
                .Where(x => x.OwnerId == currentUser.UserId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(8)
                .Select(x => new DashboardPetViewModel
                {
                    PetId = x.PetId,
                    Name = x.Name,
                    Species = x.Species,
                    Breed = x.Breed,
                    Gender = x.Gender,
                    BranchName = x.Branch.BranchName,
                    HealthStatus = x.HealthStatus,
                    VaccinationStatus = x.VaccinationStatus,
                    PrimaryImageUrl = x.PetImages
                        .OrderByDescending(i => i.IsPrimary)
                        .ThenByDescending(i => i.UploadedAt)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault(),
                    Status = x.Status,
                    AdoptionStatus = x.AdoptionStatus
                })
                .ToListAsync();

            var upcomingAppointments = await _context.Appointments
                .AsNoTracking()
                .Where(x => x.UserId == currentUser.UserId && x.AppointmentDateTime >= DateTime.Now)
                .OrderBy(x => x.AppointmentDateTime)
                .Take(8)
                .Select(x => new DashboardAppointmentViewModel
                {
                    AppointmentId = x.AppointmentId,
                    AppointmentDateTime = x.AppointmentDateTime,
                    PetName = x.Pet.Name,
                    Status = x.Status,
                    ServiceName = x.AppointmentServices
                        .OrderBy(s => s.Service.ServiceName)
                        .Select(s => s.Service.ServiceName)
                        .FirstOrDefault() ?? "Chua chon dich vu"
                })
                .ToListAsync();

            return View(new UserDashboardViewModel
            {
                FullName = currentUser.FullName,
                Email = currentUser.Email,
                Pets = myPets,
                UpcomingAppointments = upcomingAppointments
            });
        }

        public async Task<IActionResult> Profile()
        {
            var currentUser = await ResolveCurrentUserAsync();
            if (currentUser is null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(new UserProfileViewModel
            {
                FullName = currentUser.FullName,
                Phone = currentUser.Phone,
                Email = currentUser.Email,
                AvatarUrl = HttpContext.Session.GetString("ProfileAvatarUrl")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel input)
        {
            var currentUser = await ResolveCurrentUserAsync(track: true);
            if (currentUser is null)
            {
                return RedirectToAction("Login", "Account");
            }

            currentUser.FullName = (input.FullName ?? string.Empty).Trim();
            currentUser.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
            currentUser.Email = (input.Email ?? string.Empty).Trim();
            HttpContext.Session.SetString("ProfileAvatarUrl", (input.AvatarUrl ?? string.Empty).Trim());
            HttpContext.Session.SetString(ClaimNames.FullName, currentUser.FullName);

            await _context.SaveChangesAsync();
            await RefreshUserClaimsAsync(currentUser);
            TempData["ProfileSaved"] = "Da cap nhat ho so thanh cong.";
            return RedirectToAction(nameof(Profile));
        }

        private async Task RefreshUserClaimsAsync(User currentUser)
        {
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult?.Principal?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var role = authResult.Principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            var username = authResult.Principal.FindFirstValue(ClaimTypes.Name) ?? currentUser.Username;
            var staffId = authResult.Principal.FindFirstValue("StaffId");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, currentUser.UserId.ToString()),
                new(ClaimTypes.Name, username ?? string.Empty),
                new(ClaimNames.UserId, currentUser.UserId.ToString()),
                new(ClaimNames.FullName, currentUser.FullName)
            };

            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (!string.IsNullOrWhiteSpace(staffId))
            {
                claims.Add(new Claim("StaffId", staffId));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authResult.Properties ?? new AuthenticationProperties
                {
                    AllowRefresh = true,
                    IsPersistent = false
                });
        }

        private async Task<User?> ResolveCurrentUserAsync(bool track = false)
        {
            var claimValue = User.FindFirstValue(ClaimNames.UserId) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claimValue, out var userId))
            {
                return null;
            }

            var query = _context.Users.AsQueryable();
            if (!track)
            {
                query = query.AsNoTracking();
            }

            return await query.FirstOrDefaultAsync(x => x.UserId == userId && x.Status == "Active");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

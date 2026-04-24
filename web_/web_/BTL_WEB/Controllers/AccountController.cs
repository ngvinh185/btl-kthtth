using System.Security.Claims;
using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Services;
using BTL_WEB.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_WEB.Controllers;

public class AccountController : Controller
{
    private readonly PetCareHubContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public AccountController(PetCareHubContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboardByRole();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDashboardByRole();
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        model.Username = model.Username.Trim();
        model.FullName = model.FullName.Trim();
        model.Email = model.Email.Trim();
        model.Phone = model.Phone?.Trim();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedUsername = model.Username.ToLower();
        var normalizedEmail = model.Email.ToLower();

        if (await _context.Users.AnyAsync(x => x.Username.ToLower() == normalizedUsername))
        {
            ModelState.AddModelError(nameof(model.Username), "Ten dang nhap da ton tai.");
            return View(model);
        }

        if (await _context.Users.AnyAsync(x => x.Email.ToLower() == normalizedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "Email da duoc su dung.");
            return View(model);
        }

        var customerRole = await _context.Roles.FirstOrDefaultAsync(x => x.RoleName == RoleNames.Customer);
        if (customerRole is null)
        {
            ModelState.AddModelError(string.Empty, "Chua co role Customer trong database.");
            return View(model);
        }

        var user = new User
        {
            Username = model.Username,
            PasswordHash = _passwordHasher.Hash(model.Password),
            FullName = model.FullName,
            Email = model.Email,
            Phone = model.Phone,
            RoleId = customerRole.RoleId,
            CreatedAt = DateTime.Now,
            Status = "Active"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Dang ky thanh cong. Ban co the dang nhap ngay bay gio.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.Username = model.Username.Trim();
        model.Password = model.Password.Trim();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users
            .Include(x => x.Role)
            .Include(x => x.Staff)
            .FirstOrDefaultAsync(x =>
                x.Username.ToLower() == model.Username.ToLower() &&
                x.Status.ToLower() == "active");

        if (user is null || !_passwordHasher.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Thong tin dang nhap khong hop le.");
            return View(model);
        }

        if (string.Equals(user.PasswordHash, model.Password, StringComparison.Ordinal))
        {
            user.PasswordHash = _passwordHasher.Hash(model.Password);
            await _context.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.RoleName),
            new(ClaimNames.UserId, user.UserId.ToString()),
            new(ClaimNames.FullName, user.FullName)
        };

        if (user.Staff is not null)
        {
            claims.Add(new Claim("StaffId", user.Staff.StaffId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 72 : 8)
            });

        HttpContext.Session.SetString(ClaimNames.UserId, user.UserId.ToString());
        HttpContext.Session.SetString(ClaimNames.FullName, user.FullName);
        HttpContext.Session.SetString(ClaimTypes.Role, user.Role.RoleName);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToDashboardByRole();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToDashboardByRole()
    {
        if (User.IsInRole(RoleNames.Admin) || User.IsInRole(RoleNames.Staff))
        {
            return RedirectToAction("System", "Management");
        }

        return RedirectToAction("Index", "Home");
    }
}

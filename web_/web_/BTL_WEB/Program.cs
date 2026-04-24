using BTL_WEB.Helpers;
using BTL_WEB.Models;
using BTL_WEB.Resources;
using BTL_WEB.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddDbContext<PetCareHubContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PetCareHubConnection")));

var dataProtectionBuilder = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys")));

if (OperatingSystem.IsWindows())
{
    dataProtectionBuilder.ProtectKeysWithDpapi();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IPetService, PetService>();
builder.Services.AddScoped<IAppointmentWorkflowService, AppointmentWorkflowService>();
builder.Services.AddScoped<IAdoptionWorkflowService, AdoptionWorkflowService>();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".PetCareHub.Auth";
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.IsAjaxRequest())
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Vui lòng đăng nhập trước khi thực hiện thao tác này."
                });
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.IsAjaxRequest())
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Tài khoản của bạn không có quyền thực hiện thao tác này."
                });
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(RoleNames.AdminOnly, policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy(RoleNames.StaffOrAdmin, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Staff));
    options.AddPolicy(RoleNames.AllRoles, policy => policy.RequireRole(RoleNames.Admin, RoleNames.Staff, RoleNames.Customer));
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.Name = ".PetCareHub.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".PetCareHub.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource));
    });

var app = builder.Build();

var supportedCultures = new[]
{
    new CultureInfo("vi-VN"),
    new CultureInfo("en-US")
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("vi-VN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    // In development show detailed exceptions to help debug
    app.UseDeveloperExceptionPage();
}
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Lightweight health endpoint to verify the app is responsive without involving EF or MVC
app.MapGet("/health", () => Results.Ok(new { status = "OK" }));

app.Run();

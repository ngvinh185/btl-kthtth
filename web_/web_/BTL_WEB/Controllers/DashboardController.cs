using BTL_WEB.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTL_WEB.Controllers;

[Authorize(Policy = RoleNames.StaffOrAdmin)]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("System", "Management");
    }
}

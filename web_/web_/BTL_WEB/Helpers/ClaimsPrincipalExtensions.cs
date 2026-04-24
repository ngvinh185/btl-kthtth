using System.Security.Claims;

namespace BTL_WEB.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimNames.UserId);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    public static string? GetFullName(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimNames.FullName);
    }

    public static int? GetStaffId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("StaffId");
        return int.TryParse(value, out var staffId) ? staffId : null;
    }
}

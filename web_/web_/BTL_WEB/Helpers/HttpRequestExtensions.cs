namespace BTL_WEB.Helpers;

public static class HttpRequestExtensions
{
    public static bool IsAjaxRequest(this HttpRequest request)
    {
        if (request == null)
        {
            return false;
        }

        if (request.Headers.TryGetValue("X-Requested-With", out var xrw) &&
            string.Equals(xrw.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Headers.TryGetValue("Accept", out var accept) &&
            accept.ToString().IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }
}

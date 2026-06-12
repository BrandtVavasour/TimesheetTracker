namespace TimesheetTracker.Web.Services;

/// <summary>
/// Resolves the real client IP for rate-limit partitioning. Behind the
/// Cloudflare tunnel, <c>Connection.RemoteIpAddress</c> is the cloudflared
/// container IP for every request, so prefer Cloudflare's <c>CF-Connecting-IP</c>
/// (then the first <c>X-Forwarded-For</c> hop) to avoid bucketing all users
/// together.
/// </summary>
public static class ClientIp
{
    public static string For(HttpContext context)
    {
        var cf = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(cf)) return cf.Trim();

        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

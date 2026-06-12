namespace TimesheetTracker.Web.Services;

/// <summary>
/// Identifies requests for hidden/dotfiles (e.g. /.env, /.git/config) — the
/// payload of the most common automated secret-scraping bots. The static file
/// provider already excludes these, but an explicit guard returns a clean,
/// typed 404 (so browsers don't "download" an empty response) and is
/// defence-in-depth against anything ever landing in the web root.
/// </summary>
public static class HiddenPath
{
    public static bool IsBlocked(string? path) =>
        !string.IsNullOrEmpty(path)
        && path.Split('/', StringSplitOptions.RemoveEmptyEntries)
               .Any(segment => segment.StartsWith('.'));
}

public static class HiddenPathMiddleware
{
    public static IApplicationBuilder UseHiddenPathGuard(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (HiddenPath.IsBlocked(context.Request.Path.Value))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync("Not Found");
                return;
            }
            await next();
        });
}

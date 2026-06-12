namespace TimesheetTracker.Web.Services;

/// <summary>
/// Resolves a post-login / post-action redirect target to a guaranteed
/// same-origin local path, defeating open-redirect attacks via query-supplied
/// ReturnUrl values (e.g. <c>//evil.com</c>, <c>https://evil.com</c>,
/// backslash tricks, or non-http schemes).
/// </summary>
public static class SafeRedirect
{
    public static string ToLocal(string? uri, string baseUri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return "/";
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var b)) return "/";

        // Browsers treat backslashes as forward slashes, so "/\evil.com" is an
        // off-origin reference — normalise before resolving so the origin check
        // can't be bypassed.
        var candidate = uri.Trim().Replace('\\', '/');

        // Resolve the (relative or absolute) candidate against the app's base.
        // Only keep it if it lands on the exact same scheme + host + port.
        if (Uri.TryCreate(b, candidate, out var resolved)
            && resolved.Scheme == b.Scheme
            && resolved.Authority == b.Authority)
        {
            return resolved.PathAndQuery + resolved.Fragment;
        }

        return "/";
    }
}

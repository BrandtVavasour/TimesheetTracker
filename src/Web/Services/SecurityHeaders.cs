using System.Security.Cryptography;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// Adds a Content-Security-Policy with a per-request script nonce, plus the
/// standard hardening headers. The nonce is exposed via <see cref="GetNonce"/>
/// so App.razor can stamp it onto the framework/app script tags.
/// </summary>
public static class SecurityHeaders
{
    private const string NonceKey = "csp-nonce";

    /// <summary>The CSP nonce for this request (created by the middleware).</summary>
    public static string GetNonce(this HttpContext context) =>
        context.Items[NonceKey] as string ?? "";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            context.Items[NonceKey] = nonce;

            var headers = context.Response.Headers;

            // Inline style attributes are core to the app's design system, so styles
            // allow 'unsafe-inline' (nonces cannot cover style="" attributes).
            // Scripts are strictly self + nonce. ws:/wss: is the Blazor circuit.
            // form-action includes Google because the external-login POST redirects
            // to accounts.google.com and browsers validate redirects against it.
            headers.ContentSecurityPolicy =
                "default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}'; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data:; " +
                "connect-src 'self' ws: wss:; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self' https://accounts.google.com;";

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            await next();
        });
}

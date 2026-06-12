using System.Net;
using System.Reflection;

namespace TimesheetTracker.Web.Services;

/// <summary>A ready-to-send email: subject plus matching HTML and plain-text bodies.</summary>
public sealed record EmailMessage(string Subject, string Html, string Text);

/// <summary>
/// Renders branded HTML emails from the embedded templates (header bar, white
/// content card, footer), ported from the VendingTracker Manager template and
/// recoloured to the Timesheet Tracker palette.
/// </summary>
public static class EmailTemplate
{
    public const string AppName = "Timesheet Tracker";

    private static readonly Assembly Asm = typeof(EmailTemplate).Assembly;
    private static string? shell;
    private static string? button;

    /// <summary>
    /// Full HTML email: heading + subheading, with EITHER an action button
    /// (<paramref name="buttonUrl"/>) OR a highlighted code block
    /// (<paramref name="code"/>), or neither.
    /// </summary>
    public static string Render(string heading, string subheading,
        string? buttonText = null, string? buttonUrl = null, string? code = null)
    {
        var content = code is not null ? CodeBlock(code)
            : buttonUrl is not null ? Button(buttonText ?? "Open", buttonUrl)
            : string.Empty;

        return Shell()
            .Replace("{{app_title}}", AppName)
            .Replace("{{heading}}", heading)
            .Replace("{{subheading}}", subheading)
            .Replace("{{button_content}}", content);
    }

    private static string Button(string text, string url) =>
        ButtonShell().Replace("{{button_text}}", text).Replace("{{button_url}}", url);

    private static string CodeBlock(string code) =>
        $"""
        <table width="100%" border="0" cellpadding="0" cellspacing="0" role="presentation"><tr>
          <td align="center" style="padding:2px 0 0 0;">
            <div style="display:inline-block;font-family:'JetBrains Mono',Consolas,monospace;font-size:26px;font-weight:700;letter-spacing:6px;color:#0f3d28;background:#e8f6ee;border:1px solid #cdebd9;border-radius:10px;padding:16px 26px;">{code}</div>
          </td>
        </tr></table>
        """;

    private static string Shell() => shell ??= Load("EmailTemplate.html");
    private static string ButtonShell() => button ??= Load("ButtonTemplate.html");

    private static string Load(string file)
    {
        var name = $"TimesheetTracker.Web.EmailTemplates.{file}";
        using var stream = Asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded email template '{name}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>Builds the concrete Identity emails (subject + HTML + plain text).</summary>
public static class EmailMessages
{
    public static EmailMessage Confirmation(string link) => Build(
        subject: "Confirm your email",
        heading: "Confirm your email",
        subheading: $"Welcome to {EmailTemplate.AppName}. Confirm your email address to finish setting up your account.",
        buttonText: "Confirm email", url: link);

    public static EmailMessage PasswordReset(string link) => Build(
        subject: "Reset your password",
        heading: "Reset your password",
        subheading: "We received a request to reset your password. If this wasn't you, you can safely ignore this email.",
        buttonText: "Reset password", url: link);

    public static EmailMessage PasswordResetCode(string code) => Build(
        subject: "Your password reset code",
        heading: "Reset your password",
        subheading: "Use the code below to reset your password.",
        code: code);

    private static EmailMessage Build(string subject, string heading, string subheading,
        string? buttonText = null, string? url = null, string? code = null)
    {
        var html = EmailTemplate.Render(heading, subheading, buttonText, url, code);
        var text = PlainText(heading, subheading, buttonText, url, code);
        return new(subject, html, text);
    }

    private static string PlainText(string heading, string body, string? action, string? url, string? code)
    {
        // The link arrives HTML-encoded (for the href); decode it for plain text.
        var actionLine = code is not null ? $"Your code: {code}"
            : url is not null ? $"{action}: {WebUtility.HtmlDecode(url)}"
            : null;

        var parts = new[] { heading, body, actionLine, $"— {EmailTemplate.AppName}" }
            .Where(s => !string.IsNullOrEmpty(s));
        return string.Join("\n\n", parts);
    }
}

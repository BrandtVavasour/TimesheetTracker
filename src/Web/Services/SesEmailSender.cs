using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.AspNetCore.Identity;

namespace TimesheetTracker.Web.Services;

/// <summary>Sends Identity emails (confirmation, password reset) via AWS SES.</summary>
public sealed class SesEmailSender(IAmazonSimpleEmailService ses, IConfiguration config, ILogger<SesEmailSender> logger)
    : IEmailSender<AppUser>
{
    private string FromAddress => Environment.GetEnvironmentVariable("SES_FROM_EMAIL")
        ?? config["Email:FromAddress"]
        ?? "noreply@jabtech.com.au";

    private const string AppName = "Timesheet Tracker";

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your email",
            Body("Confirm your email", $"Welcome to {AppName}. Confirm your email address to finish setting up your account.",
                "Confirm email", confirmationLink));

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password",
            Body("Reset your password", "We received a request to reset your password. If this wasn't you, you can ignore this email.",
                "Reset password", resetLink));

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        SendAsync(email, "Your password reset code",
            Body("Reset your password", "Use the code below to reset your password.", null, null,
                code: resetCode));

    private async Task SendAsync(string to, string subject, string htmlBody)
    {
        var request = new SendEmailRequest
        {
            Source = $"\"{AppName}\" <{FromAddress}>",
            Destination = new()
                { ToAddresses = [to] },
            Message = new()
            {
                Subject = new(subject),
                Body = new()
                    { Html = new(htmlBody) },
            },
        };
        try
        {
            await ses.SendEmailAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send '{Subject}' email to {To}", subject, to);
            throw;
        }
    }

    private static string Body(string heading, string intro, string? buttonText, string? buttonUrl, string? code = null)
    {
        var action = code is not null
            ? $"<p style=\"font:600 26px/1 monospace;letter-spacing:4px;color:#0f3d28;background:#e8f6ee;border:1px solid #cdebd9;border-radius:8px;padding:14px;text-align:center\">{code}</p>"
            : $"<p style=\"text-align:center;margin:28px 0\"><a href=\"{buttonUrl}\" style=\"display:inline-block;background:#1f9d63;color:#fff;font:600 15px sans-serif;text-decoration:none;padding:12px 22px;border-radius:7px\">{buttonText}</a></p>";

        return $$"""
        <div style="max-width:480px;margin:0 auto;font-family:sans-serif;color:#19201e">
          <h1 style="font-size:20px">{{heading}}</h1>
          <p style="font-size:14px;color:#545d5b;line-height:1.5">{{intro}}</p>
          {{action}}
          <p style="font-size:12px;color:#8a9291">— Timesheet Tracker</p>
        </div>
        """;
    }
}

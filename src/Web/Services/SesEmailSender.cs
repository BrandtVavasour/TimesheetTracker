using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.AspNetCore.Identity;

namespace TimesheetTracker.Web.Services;

/// <summary>Sends Identity emails (confirmation, password reset) via AWS SES,
/// using the branded <see cref="EmailTemplate"/> with a plain-text alternative.</summary>
public sealed class SesEmailSender(IAmazonSimpleEmailService ses, IConfiguration config, ILogger<SesEmailSender> logger)
    : IEmailSender<AppUser>
{
    private string FromAddress => Environment.GetEnvironmentVariable("SES_FROM_EMAIL")
        ?? config["Email:FromAddress"]
        ?? "noreply@jabtech.com.au";

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        SendAsync(email, EmailMessages.Confirmation(confirmationLink));

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink) =>
        SendAsync(email, EmailMessages.PasswordReset(resetLink));

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        SendAsync(email, EmailMessages.PasswordResetCode(resetCode));

    private async Task SendAsync(string to, EmailMessage message)
    {
        var request = new SendEmailRequest
        {
            Source = $"\"{EmailTemplate.AppName}\" <{FromAddress}>",
            Destination = new() { ToAddresses = [to] },
            Message = new()
            {
                Subject = new(message.Subject),
                Body = new()
                {
                    Html = new() { Charset = "UTF-8", Data = message.Html },
                    Text = new() { Charset = "UTF-8", Data = message.Text },
                },
            },
        };

        try
        {
            await ses.SendEmailAsync(request);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send '{Subject}' email to {To}", message.Subject, to);
            throw;
        }
    }
}

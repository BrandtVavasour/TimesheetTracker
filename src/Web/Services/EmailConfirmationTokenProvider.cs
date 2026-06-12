using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// Options for the email-confirmation token provider. The token lifespan is set
/// to the verification grace window so the original link in the sign-up email
/// stays valid for the entire period a new account is allowed to use the app.
/// </summary>
public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public EmailConfirmationTokenProviderOptions()
    {
        Name = "EmailConfirmDP";
        TokenLifespan = AccountGrace.Period;
    }
}

/// <summary>A data-protection token provider dedicated to email confirmation, so
/// its (longer) lifespan doesn't weaken password-reset tokens, which keep the
/// default short lifespan.</summary>
public sealed class EmailConfirmationTokenProvider<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<EmailConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<TUser>> logger)
    : DataProtectorTokenProvider<TUser>(dataProtectionProvider, options, logger)
    where TUser : class;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace TimesheetTracker.Web.Services;

/// <summary>How the signed-in user can authenticate.</summary>
public sealed record AccountMethods(bool HasGoogle, bool HasPassword);

/// <summary>Resolves the signed-in user's linked sign-in methods.</summary>
public interface IAccountInfo
{
    Task<AccountMethods> GetMethodsAsync();
}

public sealed class AccountInfo(AuthenticationStateProvider authStateProvider, UserManager<AppUser> users) : IAccountInfo
{
    public async Task<AccountMethods> GetMethodsAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        var user = await users.GetUserAsync(state.User);
        if (user is null) return new(false, false);

        var logins = await users.GetLoginsAsync(user);
        var hasGoogle = logins.Any(l => l.LoginProvider == "Google");
        var hasPassword = await users.HasPasswordAsync(user);
        return new(hasGoogle, hasPassword);
    }
}

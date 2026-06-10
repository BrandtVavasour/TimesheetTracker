using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace TimesheetTracker.Web.Services;

/// <summary>The signed-in user's id, resolved from the authentication state.</summary>
public interface ICurrentUser
{
    Task<Guid?> GetIdAsync();
}

public sealed class CurrentUser(AuthenticationStateProvider authStateProvider) : ICurrentUser
{
    public async Task<Guid?> GetIdAsync()
    {
        var state = await authStateProvider.GetAuthenticationStateAsync();
        var id = state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }
}

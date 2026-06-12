using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace TimesheetTracker.Web.Services;

/// <summary>A flattened, display-ready view of an account for the admin screen.</summary>
public record AdminUserView(
    Guid Id,
    string DisplayName,
    string Email,
    bool EmailConfirmed,
    bool IsAdmin,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    int AccessFailedCount,
    int MaxFailedAccessAttempts,
    bool HasPassword);

public interface IAdminService
{
    /// <summary>Every account in the system, ordered by email, with status flags.</summary>
    Task<IReadOnlyList<AdminUserView>> GetUsersAsync(CancellationToken cancel = default);

    /// <summary>Clears any lockout on the user and resets their failed-attempt count.
    /// Returns false if no such user exists.</summary>
    Task<bool> UnlockAsync(Guid userId);
}

public sealed class AdminService(
    UserManager<AppUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    IOptions<IdentityOptions> identityOptions) : IAdminService
{
    public async Task<IReadOnlyList<AdminUserView>> GetUsersAsync(CancellationToken cancel = default)
    {
        // GetUsersInRoleAsync throws if the role has never been created (e.g. no
        // ADMIN_EMAIL was ever configured), so guard on its existence first.
        var adminIds = await roles.RoleExistsAsync(AdminBootstrap.AdminRole)
            ? (await users.GetUsersInRoleAsync(AdminBootstrap.AdminRole)).Select(u => u.Id).ToHashSet()
            : [];

        var all = await users.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(cancel);

        var now = DateTimeOffset.UtcNow;
        var maxFailed = identityOptions.Value.Lockout.MaxFailedAccessAttempts;

        return all.Select(u => new AdminUserView(
            u.Id,
            u.DisplayName,
            u.Email ?? "—",
            u.EmailConfirmed,
            adminIds.Contains(u.Id),
            IsLockedOut: u.LockoutEnabled && u.LockoutEnd is { } end && end > now,
            u.LockoutEnd,
            u.AccessFailedCount,
            maxFailed,
            HasPassword: !string.IsNullOrEmpty(u.PasswordHash))).ToList();
    }

    public async Task<bool> UnlockAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return false;

        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return true;
    }
}

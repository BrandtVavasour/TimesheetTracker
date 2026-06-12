using Microsoft.AspNetCore.Identity;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// Startup grant of the <see cref="AdminRole"/> to the operator-configured admin
/// email(s) (the <c>ADMIN_EMAIL</c> env var). Idempotent — safe to run on every
/// boot. If no email is configured it breaks out immediately without provisioning
/// the role or touching any user, so an unconfigured deployment has zero admins.
/// </summary>
public static class AdminBootstrap
{
    public const string AdminRole = "Admin";

    /// <summary>
    /// Parses <paramref name="adminEmails"/> (comma/semicolon separated, case-
    /// insensitive) and grants <see cref="AdminRole"/> to each matching existing
    /// user. Emails with no matching user are skipped (they may register later —
    /// a subsequent startup will pick them up).
    /// </summary>
    public static async Task EnsureAdminsAsync(
        IServiceProvider services, string? adminEmails, ILogger logger, CancellationToken cancel = default)
    {
        var emails = (adminEmails ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (emails.Count == 0)
        {
            logger.LogWarning("ADMIN_EMAIL is not set — skipping admin role assignment. No admin will be granted.");
            return;
        }

        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var users = services.GetRequiredService<UserManager<AppUser>>();

        if (!await roles.RoleExistsAsync(AdminRole))
        {
            await roles.CreateAsync(new(AdminRole));
            logger.LogInformation("Created {Role} role.", AdminRole);
        }

        foreach (var email in emails)
        {
            cancel.ThrowIfCancellationRequested();
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                logger.LogInformation(
                    "Admin email {Email} has no matching user yet — will assign on a later startup once they register.",
                    email);
                continue;
            }

            if (await users.IsInRoleAsync(user, AdminRole))
                continue;

            var result = await users.AddToRoleAsync(user, AdminRole);
            if (result.Succeeded)
                logger.LogInformation("Granted {Role} to {Email}.", AdminRole, email);
            else
                logger.LogError("Failed to grant {Role} to {Email}: {Errors}", AdminRole, email,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}

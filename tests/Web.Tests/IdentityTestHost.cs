using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel;

namespace Web.Tests;

/// <summary>
/// Minimal real ASP.NET Identity stack (UserManager + RoleManager) backed by an
/// InMemory <see cref="TimesheetDbContext"/>, so admin-role and lockout behaviour
/// is exercised through the genuine Identity APIs rather than hand-rolled DB pokes.
/// Lockout options mirror Program.cs (5 attempts / 15 min).
/// </summary>
internal sealed class IdentityTestHost : IDisposable
{
    public ServiceProvider Provider { get; }

    public IdentityTestHost()
    {
        var dbName = "admin-" + Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TimesheetDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                // Relax password rules so tests can create users with simple secrets.
                o.Password.RequiredLength = 1;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TimesheetDbContext>();
        Provider = services.BuildServiceProvider();
    }

    public UserManager<AppUser> Users => Provider.GetRequiredService<UserManager<AppUser>>();
    public RoleManager<IdentityRole<Guid>> Roles => Provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    public async Task<AppUser> CreateUserAsync(string email, string? password = null,
        bool lockedOut = false, int failedCount = 0, bool emailConfirmed = true)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = email.Split('@')[0],
            EmailConfirmed = emailConfirmed,
        };
        var result = password is null
            ? await Users.CreateAsync(user)
            : await Users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Create failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));

        if (lockedOut || failedCount > 0)
        {
            user.AccessFailedCount = failedCount;
            if (lockedOut) user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
            await Users.UpdateAsync(user);
        }
        return user;
    }

    public void Dispose() => Provider.Dispose();
}

using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.Web.Components.Pages;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

[TestFixture]
public class AdminPageRenderTests
{
    private sealed class FakeAdminService : IAdminService
    {
        public List<AdminUserView> Users { get; } = [];
        public List<Guid> Unlocked { get; } = [];

        public Task<IReadOnlyList<AdminUserView>> GetUsersAsync(CancellationToken cancel = default)
            => Task.FromResult<IReadOnlyList<AdminUserView>>(Users);

        public Task<bool> UnlockAsync(Guid userId)
        {
            Unlocked.Add(userId);
            var i = Users.FindIndex(u => u.Id == userId);
            if (i < 0) return Task.FromResult(false);
            Users[i] = Users[i] with { IsLockedOut = false, AccessFailedCount = 0, LockoutEnd = null };
            return Task.FromResult(true);
        }
    }

    private static BunitContext NewContext(FakeAdminService fake)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddLogging();
        ctx.Services.AddScoped<IToastService, ToastService>();
        ctx.Services.AddSingleton<IAdminService>(fake);
        return ctx;
    }

    private static AdminUserView View(string email, bool admin = false, bool locked = false, int failed = 0) =>
        new(Guid.NewGuid(), email.Split('@')[0], email, EmailConfirmed: true, IsAdmin: admin,
            IsLockedOut: locked, LockoutEnd: locked ? DateTimeOffset.UtcNow.AddMinutes(15) : null,
            AccessFailedCount: failed, MaxFailedAccessAttempts: 5, HasPassword: true);

    [Test]
    public void Admin_ListsUsers_WithAdminAndLockedBadgesAndReason()
    {
        var fake = new FakeAdminService();
        fake.Users.Add(View("boss@example.com", admin: true));
        fake.Users.Add(View("locked@example.com", locked: true, failed: 5));
        fake.Users.Add(View("normal@example.com"));
        using var ctx = NewContext(fake);

        var cut = ctx.Render<Admin>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("boss@example.com");
        cut.Markup.Should().Contain("locked@example.com");
        cut.Markup.Should().Contain("Admin");
        cut.Markup.Should().Contain("Locked");
        cut.Markup.Should().Contain("failed sign-ins"); // the exposed lockout reason
        cut.FindAll("button").Should().Contain(b => b.TextContent.Contains("Unlock"));
    }

    [Test]
    public void Admin_UnlockButton_CallsServiceAndClearsLock()
    {
        var fake = new FakeAdminService();
        fake.Users.Add(View("locked@example.com", locked: true, failed: 5));
        using var ctx = NewContext(fake);
        var cut = ctx.Render<Admin>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Unlock")).Click();

        cut.WaitForState(() => fake.Unlocked.Count == 1, TimeSpan.FromSeconds(10));
        fake.Unlocked.Should().ContainSingle();
        // After unlock + reload the row is no longer locked, so no Unlock button remains.
        cut.WaitForState(() => !cut.FindAll("button").Any(b => b.TextContent.Contains("Unlock")),
            TimeSpan.FromSeconds(10));
    }
}

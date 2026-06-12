using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.Web.Components.Layout;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Verifies the email-verification grace gate enforced in MainLayout: blocked
/// (unverified + past the window) users are bounced to the verify page; within
/// the window they get a reminder banner; verified users see neither.
/// </summary>
[TestFixture]
public class MainLayoutGraceTests
{
    private sealed class StubClock(DateOnly today) : IClock { public DateOnly Today => today; }
    private sealed class StubCurrentUser(Guid id) : ICurrentUser { public Task<Guid?> GetIdAsync() => Task.FromResult<Guid?>(id); }

    private static BunitContext NewContext(bool emailConfirmed, DateTimeOffset createdAt)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var userId = Guid.NewGuid();
        var dbName = "ml-" + Guid.NewGuid();
        ctx.Services.AddDbContextFactory<TimesheetDbContext>(o => o.UseInMemoryDatabase(dbName));
        ctx.Services.AddScoped<TimesheetDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<TimesheetDbContext>>().CreateDbContext());
        ctx.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        ctx.Services.AddScoped<IHolidayService, HolidayService>();
        ctx.Services.AddSingleton<IClock>(new StubClock(new DateOnly(2026, 6, 12)));
        ctx.Services.AddScoped<ICurrentUser>(_ => new StubCurrentUser(userId));
        ctx.Services.AddScoped<ITimesheetData, TimesheetData>();
        ctx.Services.AddScoped<IToastService, ToastService>();

        // Auth must be registered before the provider is first resolved (below).
        ctx.AddAuthorization().SetAuthorized("Test User");

        using (var scope = ctx.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TimesheetDbContext>();
            db.Users.Add(new AppUser
            {
                Id = userId, UserName = "u@example.com", Email = "u@example.com",
                DisplayName = "Test User", DefaultState = AustralianState.NSW,
                EmailConfirmed = emailConfirmed, CreatedAt = createdAt,
            });
            db.SaveChanges();
        }

        return ctx;
    }

    private static string CurrentUri(BunitContext ctx) =>
        ctx.Services.GetRequiredService<NavigationManager>().Uri;

    [Test]
    public void UnverifiedPastWindow_RedirectsToVerifyPage()
    {
        using var ctx = NewContext(emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddDays(-8));

        ctx.Render<MainLayout>();

        CurrentUri(ctx).Should().EndWith("Account/VerifyEmail");
    }

    [Test]
    public void UnverifiedWithinWindow_ShowsReminderBanner_NoRedirect()
    {
        using var ctx = NewContext(emailConfirmed: false, createdAt: DateTimeOffset.UtcNow.AddDays(-1));

        var cut = ctx.Render<MainLayout>();

        CurrentUri(ctx).Should().NotContain("VerifyEmail");
        cut.Markup.Should().Contain("Verify your email to keep access");
        cut.Markup.Should().Contain("days left");
    }

    [Test]
    public void VerifiedUser_NoBannerNoRedirect()
    {
        using var ctx = NewContext(emailConfirmed: true, createdAt: DateTimeOffset.UtcNow.AddDays(-30));

        var cut = ctx.Render<MainLayout>();

        CurrentUri(ctx).Should().NotContain("VerifyEmail");
        cut.Markup.Should().NotContain("Verify your email to keep access");
    }
}

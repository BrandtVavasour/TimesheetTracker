using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;
using TimesheetTracker.Web.Components.Pages;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Renders each screen against a seeded in-memory database and asserts the key
/// content shows. Catches render-time exceptions and data-binding regressions
/// across all five screens.
/// </summary>
[TestFixture]
public class PagesRenderTests
{
    private static BunitContext NewSeededContext(Action<IServiceCollection>? extraServices = null) =>
        NewContext(db => SeedData.SeedAsync(db).GetAwaiter().GetResult(), extraServices);

    /// <summary>A brand-new user: their account row exists, but no jobs or entries.</summary>
    private static BunitContext NewEmptyUserContext() => NewContext(db =>
    {
        db.Users.Add(SeedData.BuildUser());
        db.SaveChanges();
    });

    private static BunitContext NewContext(Action<TimesheetDbContext> seed, Action<IServiceCollection>? extraServices = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var dbName = "pages-" + Guid.NewGuid();
        // Mirror production: data layer pulls contexts from the factory; a scoped
        // context (from the factory) covers anything resolving TimesheetDbContext.
        ctx.Services.AddDbContextFactory<TimesheetDbContext>(o => o.UseInMemoryDatabase(dbName));
        ctx.Services.AddScoped<TimesheetDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<TimesheetDbContext>>().CreateDbContext());
        ctx.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        ctx.Services.AddScoped<IHolidayService, HolidayService>();
        ctx.Services.AddScoped<IExportService, ExportService>();
        ctx.Services.AddSingleton<IClock>(new StubClock(SeedData.Today));
        ctx.Services.AddScoped<ICurrentUser>(_ => new StubCurrentUser(SeedData.DemoUserId));
        ctx.Services.AddScoped<ITimesheetData, TimesheetData>();
        ctx.Services.AddScoped<IToastService, ToastService>();
        ctx.Services.AddSingleton<IAccountInfo>(new StubAccountInfo(hasGoogle: false, hasPassword: true));
        extraServices?.Invoke(ctx.Services);

        using var scope = ctx.Services.CreateScope();
        seed(scope.ServiceProvider.GetRequiredService<TimesheetDbContext>());
        return ctx;
    }

    private sealed class StubAccountInfo(bool hasGoogle, bool hasPassword) : IAccountInfo
    {
        public Task<AccountMethods> GetMethodsAsync() =>
            Task.FromResult(new AccountMethods(hasGoogle, hasPassword));
    }

    private sealed class StubCurrentUser(Guid id) : ICurrentUser
    {
        public Task<Guid?> GetIdAsync() => Task.FromResult<Guid?>(id);
    }

    private sealed class StubClock(DateOnly today) : IClock
    {
        public DateOnly Today => today;
    }

    [Test]
    public void Weekly_RendersSampleWeek()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Weekly>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("Acme Corp");
        cut.Markup.Should().Contain("King's Birthday");   // holiday via AustralianHolidays
        cut.Markup.Should().Contain("Overnight cutover");  // Friday midnight-spanning entry
        cut.Markup.Should().Contain("+1d");                // crosses-midnight badge
        cut.Markup.Should().Contain("40192");              // shown custom field (copyable)
        cut.Markup.Should().Contain("Week total");
        cut.Markup.Should().Contain("WFH");                // Wednesday's checkout-flow entry is work-from-home
        cut.Markup.Should().Contain("data-copy=\"PRJ-1234\""); // project codes are click-to-copy
        cut.FindAll(".te-row").Count.Should().Be(5);       // Acme entries in the sample week
    }

    [Test]
    public void Weekly_ExportWeekButton_TriggersDownload()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Weekly>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Export week")).Click();

        cut.WaitForState(
            () => ctx.JSInterop.Invocations.Any(i => i.Identifier == "tsDownload"),
            TimeSpan.FromSeconds(10));
        var call = ctx.JSInterop.Invocations.Single(i => i.Identifier == "tsDownload");
        call.Arguments[0]!.ToString().Should().EndWith(".xlsx");
    }

    [Test]
    public void Profile_WithGoogleLinked_ShowsConnectedWithoutDeadButton()
    {
        using var ctx = NewSeededContext(s =>
            s.AddSingleton<IAccountInfo>(new StubAccountInfo(hasGoogle: true, hasPassword: true)));
        var cut = ctx.Render<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("Connected");
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Connect");
    }

    [Test]
    public void Profile_WithoutGoogle_ShowsNotConnected_NoDeadButton()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("Not connected");
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Connect");
    }

    [Test]
    public void Profile_HasNoDotNetJargon_AndNoStaticPreferences()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().NotContain("ASP.NET");
        cut.Markup.Should().NotContain("Fixed this iteration");
        cut.Markup.Should().NotContain("Preferences");
    }

    [Test]
    public void Calendar_RendersMonthWithHoliday()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Calendar>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("June 2026");
        cut.Markup.Should().Contain("King's Birthday");
        cut.Markup.Should().Contain("This month");
    }

    [Test]
    public void Jobs_RendersEditorForFirstJob()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Jobs>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("Custom fields");
        cut.Markup.Should().Contain("Project codes");
        cut.Markup.Should().Contain("Employee #");
        cut.Markup.Should().Contain("PRJ-1234");
    }

    [Test]
    public void Export_RendersWorksheetPreview()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Export>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("Worksheet preview");
        cut.Markup.Should().Contain("Period total");
        cut.Markup.Should().Contain("Sprint planning");
        cut.Markup.Should().Contain("Download .xlsx");
    }

    [Test]
    public void Profile_RendersUserDetails()
    {
        using var ctx = NewSeededContext();
        var cut = ctx.Render<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("Alex Carter");
        cut.Markup.Should().Contain("Sign-in methods");
        cut.Markup.Should().Contain("Default holiday state");
    }

    // ---- New user with no jobs: every job-backed screen must resolve to an
    // empty state, never hang on "Loading…". Regression guard for the bug where
    // "_job is null" doubled as the loading sentinel (Jobs/Calendar/Export).

    [Test]
    public void Weekly_WithNoJobs_ShowsEmptyState()
    {
        using var ctx = NewEmptyUserContext();
        var cut = ctx.Render<Weekly>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("No jobs yet");
        cut.Markup.Should().NotContain("Loading…");
    }

    [Test]
    public void Jobs_WithNoJobs_ShowsCreateFirstJob()
    {
        using var ctx = NewEmptyUserContext();
        var cut = ctx.Render<Jobs>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("No jobs yet");
        cut.Markup.Should().Contain("Create your first job");
        cut.Markup.Should().NotContain("Loading…");
    }

    [Test]
    public void Calendar_WithNoJobs_ShowsEmptyState()
    {
        using var ctx = NewEmptyUserContext();
        var cut = ctx.Render<Calendar>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("No jobs yet");
        cut.Markup.Should().NotContain("Loading…");
    }

    [Test]
    public void Export_WithNoJobs_ShowsEmptyState()
    {
        using var ctx = NewEmptyUserContext();
        var cut = ctx.Render<Export>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        cut.Markup.Should().Contain("No jobs yet");
        cut.Markup.Should().NotContain("Loading…");
    }
}

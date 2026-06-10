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
    private static BunitContext NewSeededContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var dbName = "pages-" + Guid.NewGuid();
        ctx.Services.AddDbContext<TimesheetDbContext>(o => o.UseInMemoryDatabase(dbName));
        ctx.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        ctx.Services.AddScoped<IHolidayService, HolidayService>();
        ctx.Services.AddScoped<IExportService, ExportService>();
        ctx.Services.AddSingleton<IClock>(new StubClock(SeedData.Today));
        ctx.Services.AddScoped<ICurrentUser>(_ => new StubCurrentUser(SeedData.DemoUserId));
        ctx.Services.AddScoped<ITimesheetData, TimesheetData>();

        using var scope = ctx.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TimesheetDbContext>();
        SeedData.SeedAsync(db).GetAwaiter().GetResult();
        return ctx;
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
        cut.FindAll(".te-row").Count.Should().Be(5);       // Acme entries in the sample week
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
}

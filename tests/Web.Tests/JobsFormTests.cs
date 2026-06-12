using AngleSharp.Dom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Validation;
using TimesheetTracker.Web.Components.Pages;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// The Jobs form reference pattern: Save is gated on dirty state, and
/// FluentValidation errors block save and surface inline.
/// </summary>
[TestFixture]
public class JobsFormTests
{
    private static BunitContext Seeded()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var dbName = "jobsform-" + Guid.NewGuid();
        ctx.Services.AddDbContextFactory<TimesheetDbContext>(o => o.UseInMemoryDatabase(dbName));
        ctx.Services.AddScoped<TimesheetDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<TimesheetDbContext>>().CreateDbContext());
        ctx.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        ctx.Services.AddScoped<IHolidayService, HolidayService>();
        ctx.Services.AddSingleton<IClock>(new StubClock());
        ctx.Services.AddScoped<ICurrentUser>(_ => new StubUser());
        ctx.Services.AddScoped<ITimesheetData, TimesheetData>();
        ctx.Services.AddScoped<IToastService, ToastService>();
        ctx.Services.AddScoped<FluentValidation.IValidator<Job>, JobValidator>();
        using var scope = ctx.Services.CreateScope();
        SeedData.SeedAsync(scope.ServiceProvider.GetRequiredService<TimesheetDbContext>()).GetAwaiter().GetResult();
        return ctx;
    }

    private static IElement NameInput(IRenderedComponent<Jobs> cut) =>
        cut.FindAll("input").First(i => i.GetAttribute("placeholder") == "e.g. Acme Corp");

    private static IElement SaveButton(IRenderedComponent<Jobs> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Save changes");

    [Test]
    public void Save_DisabledUntilEdited_ThenEnabled()
    {
        using var ctx = Seeded();
        var cut = ctx.Render<Jobs>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        SaveButton(cut).HasAttribute("disabled").Should().BeTrue();   // clean form

        NameInput(cut).Input("Acme Renamed");

        SaveButton(cut).HasAttribute("disabled").Should().BeFalse();  // dirty
    }

    [Test]
    public void BlankName_OnSave_ShowsValidationError()
    {
        using var ctx = Seeded();
        var cut = ctx.Render<Jobs>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        NameInput(cut).Input("");          // dirty + invalid
        SaveButton(cut).Click();

        cut.Markup.Should().Contain("Give the job a name.");
    }

    [Test]
    public void RevertingEdit_DisablesSaveAgain()
    {
        using var ctx = Seeded();
        var cut = ctx.Render<Jobs>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        NameInput(cut).Input("Changed");
        SaveButton(cut).HasAttribute("disabled").Should().BeFalse();

        NameInput(cut).Input("Acme Corp"); // back to the seeded original
        SaveButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    private sealed class StubUser : ICurrentUser
    {
        public Task<Guid?> GetIdAsync() => Task.FromResult<Guid?>(SeedData.DemoUserId);
    }

    private sealed class StubClock : IClock
    {
        public DateOnly Today => SeedData.Today;
    }
}

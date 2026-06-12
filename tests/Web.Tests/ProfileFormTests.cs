using AngleSharp.Dom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;
using TimesheetTracker.Web.Components.Pages;
using TimesheetTracker.Web.Services;
using TimesheetTracker.Web.Validation;

namespace Web.Tests;

/// <summary>Profile uses the same forms pattern: Save gated on dirty, validation on save.</summary>
[TestFixture]
public class ProfileFormTests
{
    private static BunitContext Seeded()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var dbName = "profileform-" + Guid.NewGuid();
        ctx.Services.AddDbContextFactory<TimesheetDbContext>(o => o.UseInMemoryDatabase(dbName));
        ctx.Services.AddScoped<TimesheetDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<TimesheetDbContext>>().CreateDbContext());
        ctx.Services.AddSingleton<IClock>(new StubClock());
        ctx.Services.AddScoped<ICurrentUser>(_ => new StubUser());
        ctx.Services.AddScoped<ITimesheetData, TimesheetData>();
        ctx.Services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        ctx.Services.AddScoped<IHolidayService, HolidayService>();
        ctx.Services.AddScoped<IExportService, ExportService>();
        ctx.Services.AddScoped<IAccountExport, AccountExport>();
        ctx.Services.AddScoped<IToastService, ToastService>();
        ctx.Services.AddSingleton<IAccountInfo>(new StubAccount());
        ctx.Services.AddScoped<FluentValidation.IValidator<ProfileForm>, ProfileFormValidator>();
        using var scope = ctx.Services.CreateScope();
        SeedData.SeedAsync(scope.ServiceProvider.GetRequiredService<TimesheetDbContext>()).GetAwaiter().GetResult();
        return ctx;
    }

    private static IElement DisplayNameInput(IRenderedComponent<Profile> cut) =>
        cut.FindAll("input")[0];

    private static IElement SaveButton(IRenderedComponent<Profile> cut) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Save profile");

    [Test]
    public void Save_DisabledUntilEdited()
    {
        using var ctx = Seeded();
        var cut = ctx.Render<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        SaveButton(cut).HasAttribute("disabled").Should().BeTrue();
        DisplayNameInput(cut).Input("New Name");
        SaveButton(cut).HasAttribute("disabled").Should().BeFalse();
    }

    [Test]
    public void BlankDisplayName_OnSave_ShowsError()
    {
        using var ctx = Seeded();
        var cut = ctx.Render<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading…"), TimeSpan.FromSeconds(10));

        DisplayNameInput(cut).Input("");
        SaveButton(cut).Click();

        cut.Markup.Should().Contain("Enter a display name.");
    }

    private sealed class StubUser : ICurrentUser
    {
        public Task<Guid?> GetIdAsync() => Task.FromResult<Guid?>(SeedData.DemoUserId);
    }

    private sealed class StubClock : IClock
    {
        public DateOnly Today => SeedData.Today;
    }

    private sealed class StubAccount : IAccountInfo
    {
        public Task<AccountMethods> GetMethodsAsync() => Task.FromResult(new AccountMethods(true, true));
    }
}

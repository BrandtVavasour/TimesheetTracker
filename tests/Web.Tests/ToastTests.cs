using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.Web.Components.Shared;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Toast notifications: ToastService raises sonner-style messages which the
/// ToastHost renders (bottom of the screen), auto-dismissing after a delay.
/// </summary>
[TestFixture]
public class ToastTests
{
    [Test]
    public void Success_ShowsMessage_ThenAutoDismisses()
    {
        using var ctx = new BunitContext();
        var service = new ToastService();
        ctx.Services.AddSingleton<IToastService>(service);

        var cut = ctx.Render<ToastHost>();
        cut.Markup.Should().NotContain("Job saved");

        service.Success("Job saved", durationMs: 150);

        cut.WaitForState(() => cut.Markup.Contains("Job saved"), TimeSpan.FromSeconds(5));

        // Auto-dismisses after the duration.
        cut.WaitForState(() => !cut.Markup.Contains("Job saved"), TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Error_ShowsWithDangerTone()
    {
        using var ctx = new BunitContext();
        var service = new ToastService();
        ctx.Services.AddSingleton<IToastService>(service);

        var cut = ctx.Render<ToastHost>();
        service.Error("Couldn't save the entry", durationMs: 60_000);

        cut.WaitForState(() => cut.Markup.Contains("Couldn't save the entry"), TimeSpan.FromSeconds(5));
        cut.Markup.Should().Contain("data-tone=\"error\"");
    }

    [Test]
    public void MultipleToasts_StackInOrder()
    {
        using var ctx = new BunitContext();
        var service = new ToastService();
        ctx.Services.AddSingleton<IToastService>(service);

        var cut = ctx.Render<ToastHost>();
        service.Success("First", durationMs: 60_000);
        service.Success("Second", durationMs: 60_000);

        cut.WaitForState(() => cut.Markup.Contains("First") && cut.Markup.Contains("Second"), TimeSpan.FromSeconds(5));
        cut.Markup.IndexOf("First", StringComparison.Ordinal)
            .Should().BeLessThan(cut.Markup.IndexOf("Second", StringComparison.Ordinal));
    }
}

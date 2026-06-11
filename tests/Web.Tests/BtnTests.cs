using TimesheetTracker.Web.Components.Shared;

namespace Web.Tests;

/// <summary>
/// Btn busy behaviour: while an async OnClick is in flight the button disables
/// and shows a spinner (no double-submits); when the handler completes it
/// re-enables. Sync/instant handlers stay unaffected.
/// </summary>
[TestFixture]
public class BtnTests
{
    [Test]
    public void AsyncClick_DisablesAndSpins_ThenReEnables()
    {
        using var ctx = new BunitContext();
        var tcs = new TaskCompletionSource();
        var invocations = 0;

        var cut = ctx.Render<Btn>(p => p
            .Add(x => x.OnClick, () => { invocations++; return tcs.Task; })
            .AddChildContent("Save"));

        cut.Find("button").Click();

        // While the handler is pending: disabled + spinner shown.
        cut.WaitForState(() => cut.Find("button").HasAttribute("disabled"), TimeSpan.FromSeconds(5));
        cut.Markup.Should().Contain("ts-spin");

        // A second click while busy must not re-invoke the handler.
        cut.Find("button").Click();
        invocations.Should().Be(1);

        // Handler completes: re-enabled, spinner gone, clickable again.
        tcs.SetResult();
        cut.WaitForState(() => !cut.Find("button").HasAttribute("disabled"), TimeSpan.FromSeconds(5));
        cut.Markup.Should().NotContain("ts-spin");

        cut.Find("button").Click();
        invocations.Should().Be(2);
    }

    [Test]
    public void SyncClick_CompletesWithoutStickingBusy()
    {
        using var ctx = new BunitContext();
        var invocations = 0;

        var cut = ctx.Render<Btn>(p => p
            .Add(x => x.OnClick, () => { invocations++; })
            .AddChildContent("Go"));

        cut.Find("button").Click();
        cut.Find("button").Click();

        invocations.Should().Be(2);
        cut.Find("button").HasAttribute("disabled").Should().BeFalse();
        cut.Markup.Should().NotContain("ts-spin");
    }

    [Test]
    public void DisabledBtn_DoesNotInvokeHandler()
    {
        using var ctx = new BunitContext();
        var invocations = 0;

        var cut = ctx.Render<Btn>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.OnClick, () => { invocations++; })
            .AddChildContent("Nope"));

        cut.Find("button").Click();
        invocations.Should().Be(0);
    }
}

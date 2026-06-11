using TimesheetTracker.Web.Components.Shared;

namespace Web.Tests;

/// <summary>
/// The copy affordances must carry a <c>data-copy</c> attribute: the clipboard
/// write happens in a native capture-phase click listener (ts.js) so it runs
/// synchronously inside the browser's user gesture. Copying via Blazor JS
/// interop happens after the gesture expires, which clipboard APIs reject
/// (silently — the button still flashed "copied" while nothing was copied).
/// </summary>
[TestFixture]
public class CopyComponentTests
{
    [Test]
    public void CopyValue_RendersDataCopyAttribute()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<CopyValue>(p => p.Add(x => x.Value, "7.50"));

        cut.Find("button").GetAttribute("data-copy").Should().Be("7.50");
    }

    [Test]
    public void CopyField_RendersDataCopyAttribute()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<CopyField>(p => p
            .Add(x => x.Name, "Employee #")
            .Add(x => x.Value, "40192"));

        cut.Find("button").GetAttribute("data-copy").Should().Be("40192");
    }

    [Test]
    public void CopyValue_Click_FlashesCopiedState()
    {
        using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = ctx.Render<CopyValue>(p => p.Add(x => x.Value, "7.50"));
        cut.Find("button").Click();

        cut.WaitForState(() => cut.Markup.Contains("flashCopied"), TimeSpan.FromSeconds(5));
    }
}

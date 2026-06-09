using TimesheetTracker.Web.Components.Shared;

namespace Web.Tests;

[TestFixture]
public class ComponentSnapshotTests
{
    [Test]
    public Task Badge_Accent_RendersExpectedMarkup()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Badge>(p => p.Add(b => b.Tone, "accent").AddChildContent("Today"));
        return Verify(cut.Markup);
    }

    [Test]
    public Task Badge_Danger_WithIcon()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Badge>(p => p.Add(b => b.Tone, "danger").Add(b => b.IconName, "info").AddChildContent("check"));
        return Verify(cut.Markup);
    }

    [Test]
    public Task Tag_RendersProjectCode()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<Tag>(p => p.AddChildContent("PRJ-1234"));
        return Verify(cut.Markup);
    }

    [Test]
    public Task CopyValue_DefaultTone()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<CopyValue>(p => p.Add(c => c.Value, "8.00"));
        return Verify(cut.Markup);
    }

    [Test]
    public Task CopyField_RendersLabelAndValue()
    {
        using var ctx = new BunitContext();
        var cut = ctx.Render<CopyField>(p => p.Add(c => c.Name, "Employee #").Add(c => c.Value, "40192"));
        return Verify(cut.Markup);
    }
}

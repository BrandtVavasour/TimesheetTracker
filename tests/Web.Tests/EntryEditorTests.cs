using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.Web.Components.Shared;

namespace Web.Tests;

/// <summary>
/// New-entry time defaults: with no job defaults the start/end inputs are empty
/// and Save is disabled; with job defaults set they prefill and the totals show.
/// </summary>
[TestFixture]
public class EntryEditorTests
{
    private static BunitContext NewCtx()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton<ITimeCalculationService, TimeCalculationService>();
        return ctx;
    }

    private static Job NewJob() => new()
    {
        Id = Guid.NewGuid(), Name = "Acme", DecimalPlaces = 2,
    };

    private static TimeEntry NewDraft(Job job) => new()
    {
        Id = Guid.NewGuid(), JobId = job.Id, WorkDate = new(2026, 6, 11), BreakMinutes = 0,
    };

    private static IRenderedComponent<EntryEditor> Render(BunitContext ctx, Job job, TimeOnly? start, TimeOnly? end) =>
        ctx.Render<EntryEditor>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.IsNew, true)
            .Add(x => x.Job, job)
            .Add(x => x.Draft, NewDraft(job))
            .Add(x => x.InitialStart, start)
            .Add(x => x.InitialEnd, end));

    [Test]
    public void NewEntry_NoDefaults_HasEmptyTimesAndDisabledSave()
    {
        using var ctx = NewCtx();
        var cut = Render(ctx, NewJob(), start: null, end: null);

        var times = cut.FindAll("input[type=time]");
        times[0].GetAttribute("value").Should().BeNullOrEmpty();
        times[1].GetAttribute("value").Should().BeNullOrEmpty();

        var save = cut.FindAll("button").Single(b => b.TextContent.Contains("Save entry"));
        save.HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().Contain("Enter start and end times");
    }

    [Test]
    public void NewEntry_WithJobDefaults_PrefillsTimesAndComputesTotals()
    {
        using var ctx = NewCtx();
        var cut = Render(ctx, NewJob(), start: new TimeOnly(8, 30), end: new TimeOnly(17, 0));

        var times = cut.FindAll("input[type=time]");
        times[0].GetAttribute("value").Should().Be("08:30");
        times[1].GetAttribute("value").Should().Be("17:00");

        var save = cut.FindAll("button").Single(b => b.TextContent.Contains("Save entry"));
        save.HasAttribute("disabled").Should().BeFalse();
        cut.Markup.Should().Contain("8.50"); // 8.5h at 2dp
    }

    [Test]
    public void EditExistingEntry_ShowsItsTimes()
    {
        using var ctx = NewCtx();
        var job = NewJob();
        var draft = NewDraft(job);
        draft.StartTime = new(10, 15);
        draft.EndTime = new(14, 45);

        var cut = ctx.Render<EntryEditor>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.IsNew, false)
            .Add(x => x.Job, job)
            .Add(x => x.Draft, draft));

        var times = cut.FindAll("input[type=time]");
        times[0].GetAttribute("value").Should().Be("10:15");
        times[1].GetAttribute("value").Should().Be("14:45");
    }
}

using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Dirty tracking via a value snapshot taken on load, so the Save button can be
/// gated on "has unsaved changes" and a navigation guard can warn on exit —
/// without per-form bespoke comparison code.
/// </summary>
[TestFixture]
public class FormSnapshotTests
{
    private sealed class Model
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    [Test]
    public void FreshSnapshot_IsNotDirty()
    {
        var m = new Model { Name = "a", Count = 1, Tags = ["x"] };
        var snap = new FormSnapshot<Model>(m);
        snap.IsDirty(m).Should().BeFalse();
    }

    [Test]
    public void ScalarChange_IsDirty()
    {
        var m = new Model { Name = "a" };
        var snap = new FormSnapshot<Model>(m);
        m.Name = "b";
        snap.IsDirty(m).Should().BeTrue();
    }

    [Test]
    public void CollectionChange_IsDirty()
    {
        var m = new Model { Tags = ["x"] };
        var snap = new FormSnapshot<Model>(m);
        m.Tags.Add("y");
        snap.IsDirty(m).Should().BeTrue();
    }

    [Test]
    public void RevertingChange_IsNotDirtyAgain()
    {
        var m = new Model { Name = "a" };
        var snap = new FormSnapshot<Model>(m);
        m.Name = "b";
        snap.IsDirty(m).Should().BeTrue();
        m.Name = "a";
        snap.IsDirty(m).Should().BeFalse();
    }

    [Test]
    public void Reset_RebaselinesToCurrentState()
    {
        var m = new Model { Name = "a" };
        var snap = new FormSnapshot<Model>(m);
        m.Name = "b";
        snap.Reset(m);            // e.g. after a successful save
        snap.IsDirty(m).Should().BeFalse();
        m.Name = "c";
        snap.IsDirty(m).Should().BeTrue();
    }
}

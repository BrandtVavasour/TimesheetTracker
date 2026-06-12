using Microsoft.Extensions.Logging.Abstractions;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Shared save-with-feedback helper that replaces the copy-pasted
/// try / Toast.Success / catch+Logger.LogError+Toast.Error blocks in the forms.
/// </summary>
[TestFixture]
public class ToastActionsTests
{
    [Test]
    public async Task Success_RunsAction_RaisesSuccessToast_ReturnsTrue()
    {
        var toast = new ToastService();
        var ran = false;

        var ok = await toast.RunAsync(NullLogger.Instance, () => { ran = true; return Task.CompletedTask; },
            success: "Saved", error: "Nope", logContext: "save");

        ok.Should().BeTrue();
        ran.Should().BeTrue();
        toast.Items.Should().ContainSingle(t => t.Tone == "success" && t.Message == "Saved");
    }

    [Test]
    public async Task Failure_SwallowsException_RaisesErrorToast_ReturnsFalse()
    {
        var toast = new ToastService();

        var ok = await toast.RunAsync(NullLogger.Instance,
            () => throw new InvalidOperationException("boom"),
            success: "Saved", error: "Couldn't save", logContext: "save");

        ok.Should().BeFalse();
        toast.Items.Should().ContainSingle(t => t.Tone == "error" && t.Message == "Couldn't save");
    }
}

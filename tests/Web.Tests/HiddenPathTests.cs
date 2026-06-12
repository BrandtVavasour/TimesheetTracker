using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Guard against bot probes for dotfiles (/.env, /.git/config, /.aws/credentials).
/// Any path segment starting with a dot is treated as not-found.
/// </summary>
[TestFixture]
public class HiddenPathTests
{
    [TestCase("/.env")]
    [TestCase("/.git/config")]
    [TestCase("/.aws/credentials")]
    [TestCase("/.well-known/../.env")]
    [TestCase("/config/.env")]
    [TestCase("/.DS_Store")]
    public void HiddenSegments_AreBlocked(string path)
    {
        HiddenPath.IsBlocked(path).Should().BeTrue();
    }

    [TestCase("/")]
    [TestCase("/jobs")]
    [TestCase("/app.css")]
    [TestCase("/ts.js")]
    [TestCase("/favicon.svg")]
    [TestCase("/Account/Login")]
    [TestCase("/health/live")]
    [TestCase("")]
    public void NormalPaths_AreAllowed(string path)
    {
        HiddenPath.IsBlocked(path).Should().BeFalse();
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// AssetVersion produces a content-hash for ?v= cache busting on wwwroot
/// assets: stable for identical content, different when the file changes —
/// so a deployed fix to ts.js/app.css actually reaches browsers instead of
/// being masked by their cached copy.
/// </summary>
[TestFixture]
public class AssetVersionTests
{
    private string root = null!;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "assetver-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(root, recursive: true);

    private AssetVersion NewSut() => new(new StubEnv(root));

    [Test]
    public void SameContent_GivesStableVersion()
    {
        File.WriteAllText(Path.Combine(root, "ts.js"), "window.x = 1;");
        var sut = NewSut();

        var v1 = sut.For("ts.js");
        var v2 = sut.For("ts.js");

        v1.Should().NotBeNullOrWhiteSpace();
        v2.Should().Be(v1);
    }

    [Test]
    public void DifferentContent_GivesDifferentVersion()
    {
        File.WriteAllText(Path.Combine(root, "ts.js"), "window.x = 1;");
        var v1 = NewSut().For("ts.js");

        File.WriteAllText(Path.Combine(root, "ts.js"), "window.x = 2;");
        var v2 = NewSut().For("ts.js");

        v2.Should().NotBe(v1);
    }

    [Test]
    public void MissingFile_ReturnsFallback() =>
        NewSut().For("nope.js").Should().Be("0");

    private sealed class StubEnv(string root) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(root);
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
        public string ContentRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Development";
    }
}

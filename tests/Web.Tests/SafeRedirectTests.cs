using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Open-redirect guard. Post-login the app redirects to a query-supplied
/// ReturnUrl; this must resolve to a same-origin path only. Confirmed
/// exploitable before the fix: ReturnUrl=//evil.com bounced users off-site.
/// </summary>
[TestFixture]
public class SafeRedirectTests
{
    private const string Base = "https://timesheet.jabtech.com.au/";

    [TestCase("/jobs", "/jobs")]
    [TestCase("/jobs?week=2026-06-08", "/jobs?week=2026-06-08")]
    [TestCase("/", "/")]
    [TestCase("Account/RegisterConfirmation?email=a%40b.com", "/Account/RegisterConfirmation?email=a%40b.com")]
    [TestCase("https://timesheet.jabtech.com.au/export", "/export")]   // same-origin absolute → relative
    public void Local_AndSameOrigin_Preserved(string input, string expected) =>
        SafeRedirect.ToLocal(input, Base).Should().Be(expected);

    [TestCase("//evil.example.com")]                 // protocol-relative (the confirmed exploit)
    [TestCase("//evil.example.com/path")]
    [TestCase("https://evil.example.com")]
    [TestCase("http://evil.example.com/x")]
    [TestCase("/\\evil.example.com")]                // backslash trick (browsers treat \ as /)
    [TestCase("\\\\evil.example.com")]
    [TestCase("javascript:alert(1)")]
    [TestCase("https://timesheet.jabtech.com.au.evil.com/x")] // look-alike host
    public void OffOrigin_OrDangerous_RewrittenToRoot(string input) =>
        SafeRedirect.ToLocal(input, Base).Should().Be("/");

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void NullOrBlank_GivesRoot(string? input) =>
        SafeRedirect.ToLocal(input, Base).Should().Be("/");
}

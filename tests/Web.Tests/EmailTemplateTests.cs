using TimesheetTracker.Web.Services;

namespace Web.Tests;

[TestFixture]
public class EmailTemplateTests
{
    [Test]
    public void Render_EmbedsTemplateChrome_HeadingSubheadingAndButton()
    {
        var html = EmailTemplate.Render("Hello there", "A short subheading", "Do it", "https://example/go");

        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("Timesheet Tracker");          // header + footer brand
        html.Should().Contain("This is an automated message"); // footer
        html.Should().Contain("Hello there");                 // heading
        html.Should().Contain("A short subheading");          // subheading
        html.Should().Contain("Do it");                       // button label
        html.Should().Contain("https://example/go");          // button href
    }

    [Test]
    public void Render_WithCode_ShowsCode_AndNoButtonHref()
    {
        var html = EmailTemplate.Render("Reset", "Use this code", code: "482913");

        html.Should().Contain("482913");
        html.Should().NotContain("{{button_content}}"); // placeholder fully replaced
    }

    [Test]
    public void Confirmation_ProducesSubjectHtmlAndPlainTextWithDecodedLink()
    {
        // The link arrives HTML-encoded (as Register passes it).
        var m = EmailMessages.Confirmation("https://app/confirm?userId=1&amp;code=abc");

        m.Subject.Should().Be("Confirm your email");
        m.Html.Should().Contain("Confirm email");                              // button
        m.Html.Should().Contain("https://app/confirm?userId=1&amp;code=abc");  // encoded href

        // Plain-text alternative: real link (decoded), no markup.
        m.Text.Should().Contain("Confirm email: https://app/confirm?userId=1&code=abc");
        m.Text.Should().Contain("— Timesheet Tracker");
        m.Text.Should().NotContain("<");
    }

    [Test]
    public void PasswordReset_HasResetButtonAndLink()
    {
        var m = EmailMessages.PasswordReset("https://app/reset");

        m.Subject.Should().Be("Reset your password");
        m.Html.Should().Contain("Reset password");
        m.Html.Should().Contain("https://app/reset");
        m.Text.Should().Contain("Reset password: https://app/reset");
    }

    [Test]
    public void PasswordResetCode_ShowsCodeInBothBodies()
    {
        var m = EmailMessages.PasswordResetCode("482913");

        m.Subject.Should().Be("Your password reset code");
        m.Html.Should().Contain("482913");
        m.Text.Should().Contain("Your code: 482913");
        m.Text.Should().NotContain("<");
    }
}

using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.Web.Validation;

namespace Web.Tests;

[TestFixture]
public class ProfileFormValidatorTests
{
    private readonly ProfileFormValidator _validator = new();

    [Test]
    public void Valid_Passes()
    {
        var form = new ProfileForm { DisplayName = "Alex", DefaultState = AustralianState.NSW };
        _validator.Validate(form).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void BlankDisplayName_Fails(string name)
    {
        var form = new ProfileForm { DisplayName = name };
        _validator.Validate(form).IsValid.Should().BeFalse();
    }

    [Test]
    public void DisplayNameTooLong_Fails()
    {
        var form = new ProfileForm { DisplayName = new string('x', 101) };
        _validator.Validate(form).IsValid.Should().BeFalse();
    }
}

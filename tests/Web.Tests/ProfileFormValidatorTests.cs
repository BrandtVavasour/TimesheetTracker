using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.Web.Validation;

namespace Web.Tests;

[TestFixture]
public class ProfileFormValidatorTests
{
    private readonly ProfileFormValidator validator = new();

    [Test]
    public void Valid_Passes()
    {
        var form = new ProfileForm { DisplayName = "Alex Carter", DefaultState = AustralianState.NSW };
        validator.Validate(form).IsValid.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void BlankDisplayName_Fails(string name)
    {
        var form = new ProfileForm { DisplayName = name };
        validator.Validate(form).IsValid.Should().BeFalse();
    }

    [Test]
    public void DisplayNameTooLong_Fails()
    {
        var form = new ProfileForm { DisplayName = new('x', 101) };
        validator.Validate(form).IsValid.Should().BeFalse();
    }
}

using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.DataModel.Validation;

namespace DataModel.Tests.UnitTests;

[TestFixture]
public class JobValidatorTests
{
    private readonly JobValidator validator = new();

    private static Job ValidJob() => new()
    {
        Name = "Acme Corp",
        DecimalPlaces = 2,
        WorkDays = DaysOfWeek.Weekdays,
        CustomFields = [new() { Name = "Employee #", Value = "40192" }],
        ProjectCodes = [new() { Code = "PRJ-1234", Description = "Rebuild" }],
    };

    [Test]
    public void ValidJob_Passes() =>
        validator.Validate(ValidJob()).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("   ")]
    public void BlankName_Fails(string name)
    {
        var job = ValidJob();
        job.Name = name;
        var result = validator.Validate(job);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(Job.Name));
    }

    [Test]
    public void NameTooLong_Fails()
    {
        var job = ValidJob();
        job.Name = new('x', 201);
        validator.Validate(job).IsValid.Should().BeFalse();
    }

    [TestCase(0, true)]
    [TestCase(4, true)]
    [TestCase(5, false)]
    [TestCase(-1, false)]
    public void DecimalPlaces_RangeEnforced(int places, bool valid)
    {
        var job = ValidJob();
        job.DecimalPlaces = places;
        validator.Validate(job).IsValid.Should().Be(valid);
    }

    [Test]
    public void CustomFieldWithBlankName_Fails()
    {
        var job = ValidJob();
        job.CustomFields.Add(new() { Name = "", Value = "x" });
        validator.Validate(job).IsValid.Should().BeFalse();
    }

    [Test]
    public void ProjectCodeWithBlankCode_Fails()
    {
        var job = ValidJob();
        job.ProjectCodes.Add(new() { Code = "", Description = "x" });
        validator.Validate(job).IsValid.Should().BeFalse();
    }
}

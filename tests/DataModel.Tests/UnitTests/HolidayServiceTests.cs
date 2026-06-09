using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.DataModel.Services;

namespace DataModel.Tests.UnitTests;

[TestFixture]
public class HolidayServiceTests
{
    private readonly IHolidayService _service = new HolidayService();

    [Test]
    public void ChristmasDay_IsHoliday_InNsw()
    {
        var isHoliday = _service.IsPublicHoliday(new DateOnly(2026, 12, 25), AustralianState.NSW, out var name);
        isHoliday.Should().BeTrue();
        name.Should().Be("Christmas Day");
    }

    [Test]
    public void OrdinaryWeekday_IsNotHoliday()
    {
        var isHoliday = _service.IsPublicHoliday(new DateOnly(2026, 6, 9), AustralianState.NSW, out var name);
        isHoliday.Should().BeFalse();
        name.Should().BeNull();
    }
}

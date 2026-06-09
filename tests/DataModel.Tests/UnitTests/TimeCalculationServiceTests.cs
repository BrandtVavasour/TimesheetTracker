using TimesheetTracker.DataModel.Services;

namespace DataModel.Tests.UnitTests;

[TestFixture]
public class TimeCalculationServiceTests
{
    private readonly ITimeCalculationService _service = new TimeCalculationService();

    [Test]
    public void DurationMinutes_SimpleShift_SubtractsBreak()
    {
        var entry = Entry(start: new(9, 0), end: new(17, 0), breakMinutes: 30);
        _service.DurationMinutes(entry).Should().Be(450); // 8h - 30m
    }

    [Test]
    public void DurationMinutes_PastMidnight_AddsADay()
    {
        var entry = Entry(start: new(22, 0), end: new(6, 0), breakMinutes: 0, endsNextDay: true);
        _service.DurationMinutes(entry).Should().Be(480); // 8h
    }

    [Test]
    public void DecimalHours_RoundsToJobPrecision()
    {
        var entry = Entry(start: new(9, 0), end: new(17, 15), breakMinutes: 0); // 8.25h
        _service.DecimalHours(entry, decimalPlaces: 2).Should().Be(8.25m);
        _service.DecimalHours(entry, decimalPlaces: 1).Should().Be(8.3m);
    }

    [Test]
    public void HoursMinutes_FormatsAsHColonMM()
    {
        var entry = Entry(start: new(9, 0), end: new(16, 30), breakMinutes: 0);
        _service.HoursMinutes(entry).Should().Be("7:30");
    }

    [Test]
    public void DurationMinutes_NonPositive_ReturnsZero()
    {
        var entry = Entry(start: new(9, 0), end: new(9, 0), breakMinutes: 30);
        _service.DurationMinutes(entry).Should().Be(0);
    }

    private static TimeEntry Entry(TimeOnly start, TimeOnly end, int breakMinutes, bool endsNextDay = false) =>
        new()
        {
            StartTime = start,
            EndTime = end,
            BreakMinutes = breakMinutes,
            EndsNextDay = endsNextDay,
            WorkDate = new DateOnly(2026, 6, 8)
        };
}

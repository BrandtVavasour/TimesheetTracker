using ExcelsiorClosedXml;

namespace TimesheetTracker.DataModel.Services.Export;

public class ExportService(ITimeCalculationService calc, IHolidayService holidays) : IExportService
{
    public IReadOnlyList<TimesheetRow> BuildRows(IEnumerable<TimeEntry> entries, ExportContext context)
    {
        var rows = new List<TimesheetRow>();
        foreach (var e in entries.OrderBy(e => e.WorkDate).ThenBy(e => e.StartTime))
        {
            var isHoliday = holidays.IsPublicHoliday(e.WorkDate, context.State, out var holidayName);
            rows.Add(new TimesheetRow
            {
                Date = e.WorkDate.ToString("yyyy-MM-dd"),
                Day = e.WorkDate.DayOfWeek.ToString(),
                Start = e.StartTime.ToString("HH:mm"),
                End = e.EndTime.ToString("HH:mm"),
                BreakMinutes = e.BreakMinutes,
                DecimalHours = calc.DecimalHours(e, context.DecimalPlaces),
                HoursMinutes = calc.HoursMinutes(e),
                ProjectCode = e.ProjectCode?.Code,
                PublicHoliday = isHoliday ? holidayName : null,
                Notes = e.Notes
            });
        }

        return rows;
    }

    public async Task<byte[]> ToWorkbookAsync(IReadOnlyList<TimesheetRow> rows, CancellationToken cancel = default)
    {
        var builder = new BookBuilder();
        builder.AddSheet(rows);

        using var stream = new MemoryStream();
        await builder.ToStream(stream, cancel);
        return stream.ToArray();
    }
}

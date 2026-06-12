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
            rows.Add(new()
            {
                Date = e.WorkDate.ToString("yyyy-MM-dd"),
                Day = e.WorkDate.DayOfWeek.ToString(),
                Start = e.StartTime.ToString("HH:mm"),
                End = e.EndTime.ToString("HH:mm"),
                BreakMinutes = e.BreakMinutes,
                DecimalHours = calc.DecimalHours(e, context.DecimalPlaces),
                HoursMinutes = calc.HoursMinutes(e),
                ProjectCode = Neutralize(e.ProjectCode?.Code),
                WorkFromHome = e.IsWorkFromHome ? "Yes" : null,
                PublicHoliday = isHoliday ? holidayName : null,
                Notes = Neutralize(e.Notes)
            });
        }

        return rows;
    }

    // Spreadsheet (CSV/XLSX) formula-injection guard: a user string whose first
    // character is one Excel treats as a formula trigger is prefixed with an
    // apostrophe so the consuming spreadsheet renders it as literal text.
    private static string? Neutralize(string? value) =>
        !string.IsNullOrEmpty(value) && "=+-@\t\r".IndexOf(value[0]) >= 0 ? "'" + value : value;

    public async Task<byte[]> ToWorkbookAsync(IReadOnlyList<TimesheetRow> rows, CancellationToken cancel = default)
    {
        var builder = new BookBuilder();
        builder.AddSheet(rows);

        using var stream = new MemoryStream();
        await builder.ToStream(stream, cancel);
        return stream.ToArray();
    }

    public Task<byte[]> ToPdfAsync(IReadOnlyList<TimesheetRow> rows, ExportDocument document, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(TimesheetPdf.Render(rows, document));
    }
}

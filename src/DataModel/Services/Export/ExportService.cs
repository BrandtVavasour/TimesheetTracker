using ExcelsiorClosedXml;

namespace TimesheetTracker.DataModel.Services.Export;

public class ExportService(ITimeCalculationService calc, IHolidayService holidays) : IExportService
{
    public IReadOnlyList<TimesheetRow> BuildRows(IEnumerable<TimeEntry> entries, ExportContext context,
        DateOnly from = default, DateOnly to = default, bool includeAllDays = false)
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

        // Optionally pad every calendar day in [from, to] that has no entry with a
        // blank row (public-holiday name retained), so an export shows the whole
        // period rather than only worked days.
        if (includeAllDays && from != default && from <= to)
        {
            var present = rows.Select(r => r.Date).ToHashSet();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (present.Contains(d.ToString("yyyy-MM-dd"))) continue;
                var isHoliday = holidays.IsPublicHoliday(d, context.State, out var holidayName);
                rows.Add(new()
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Day = d.DayOfWeek.ToString(),
                    Start = "",
                    End = "",
                    BreakMinutes = 0,
                    DecimalHours = 0,
                    HoursMinutes = "0:00",
                    PublicHoliday = isHoliday ? holidayName : null,
                });
            }
            rows = rows.OrderBy(r => r.Date).ThenBy(r => r.Start).ToList();
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

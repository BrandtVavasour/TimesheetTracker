using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services.Export;

public interface IExportService
{
    IReadOnlyList<TimesheetRow> BuildRows(IEnumerable<TimeEntry> entries, ExportContext context);
    Task<byte[]> ToWorkbookAsync(IReadOnlyList<TimesheetRow> rows, CancellationToken cancel = default);
    Task<byte[]> ToPdfAsync(IReadOnlyList<TimesheetRow> rows, ExportDocument document, CancellationToken cancel = default);
}

/// <summary>The effective state + job precision needed to render rows.</summary>
public record ExportContext(AustralianState State, int DecimalPlaces);

/// <summary>
/// The header metadata a printable timesheet document needs around the rows:
/// who/which job/which period it covers, the job's decimal precision, the
/// period total (minutes), and any custom fields shown on the timesheet.
/// </summary>
public record ExportDocument(
    string JobName,
    string EmployeeName,
    AustralianState State,
    string PeriodLabel,
    int DecimalPlaces,
    int TotalMinutes,
    IReadOnlyList<ExportField> Fields);

/// <summary>A custom field name/value pair shown in the document header.</summary>
public record ExportField(string Name, string Value);

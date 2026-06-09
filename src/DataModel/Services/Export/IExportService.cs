using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services.Export;

public interface IExportService
{
    IReadOnlyList<TimesheetRow> BuildRows(IEnumerable<TimeEntry> entries, ExportContext context);
    Task<byte[]> ToWorkbookAsync(IReadOnlyList<TimesheetRow> rows, CancellationToken cancel = default);
}

/// <summary>The effective state + job precision needed to render rows.</summary>
public record ExportContext(AustralianState State, int DecimalPlaces);

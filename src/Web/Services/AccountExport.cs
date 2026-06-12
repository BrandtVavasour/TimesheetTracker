using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;

namespace TimesheetTracker.Web.Services;

public interface IAccountExport
{
    /// <summary>A ZIP of the current user's full data: <c>account.json</c> plus an
    /// <c>.xlsx</c> and a <c>.json</c> per job (every entry, archived jobs included).</summary>
    Task<byte[]> BuildZipAsync();
}

public sealed partial class AccountExport(ITimesheetData data, IExportService export, ITimeCalculationService calc)
    : IAccountExport
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<byte[]> BuildZipAsync()
    {
        var user = await data.CurrentUserAsync();
        var jobs = await data.AllJobsAsync();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedSlugs = new HashSet<string>();
            var totalEntries = 0;

            foreach (var job in jobs)
            {
                var entries = await data.AllEntriesAsync(job.Id);
                totalEntries += entries.Count;
                var slug = UniqueSlug(job.Name, job.Id, usedSlugs);

                // Human-readable workbook (reuses the export styling/columns).
                var rows = export.BuildRows(entries, new ExportContext(data.EffectiveState(job, user), job.DecimalPlaces));
                await WriteBytes(zip, $"jobs/{slug}.xlsx", await export.ToWorkbookAsync(rows));

                // Complete structured copy.
                await WriteJson(zip, $"jobs/{slug}.json", ToJobDto(job, entries));
            }

            await WriteJson(zip, "account.json", new AccountDto(
                user.DisplayName, user.Email, user.DefaultState.ToString(), user.ExportIncludeAllDays,
                user.CreatedAt, jobs.Count, totalEntries, DateTimeOffset.UtcNow));
        }

        return ms.ToArray();
    }

    private JobDto ToJobDto(Job job, IReadOnlyList<TimeEntry> entries) => new(
        job.Name,
        job.StateOverride?.ToString(),
        job.DecimalPlaces,
        job.WorkDays.ToString(),
        job.IsArchived,
        job.CustomFields.OrderBy(f => f.DisplayOrder)
            .Select(f => new FieldDto(f.Name, f.Value, f.ShowOnTimesheet)).ToList(),
        job.ProjectCodes.OrderBy(c => c.DisplayOrder)
            .Select(c => new CodeDto(c.Code, c.Description, c.IsActive)).ToList(),
        entries.Select(e => new EntryDto(
            e.WorkDate, e.StartTime, e.EndTime, e.EndsNextDay, e.BreakMinutes, e.IsWorkFromHome, e.Notes,
            e.ProjectCode?.Code, calc.DecimalHours(e, job.DecimalPlaces), calc.HoursMinutes(e))).ToList());

    private static string UniqueSlug(string name, Guid id, HashSet<string> used)
    {
        var baseSlug = Slug().Replace(name, "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "job";
        var slug = $"{baseSlug}-{id.ToString()[..8]}"; // the id suffix guarantees uniqueness
        used.Add(slug);
        return slug;
    }

    private static async Task WriteJson<T>(ZipArchive zip, string name, T value) =>
        await WriteBytes(zip, name, JsonSerializer.SerializeToUtf8Bytes(value, Json));

    private static async Task WriteBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var s = entry.Open();
        await s.WriteAsync(bytes);
    }

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex Slug();

    private record AccountDto(string DisplayName, string? Email, string DefaultState, bool ExportIncludeAllDays,
        DateTimeOffset CreatedAt, int Jobs, int Entries, DateTimeOffset ExportedAt);

    private record JobDto(string Name, string? StateOverride, int DecimalPlaces, string WorkDays, bool IsArchived,
        List<FieldDto> CustomFields, List<CodeDto> ProjectCodes, List<EntryDto> Entries);

    private record FieldDto(string Name, string Value, bool ShowOnTimesheet);

    private record CodeDto(string Code, string? Description, bool IsActive);

    private record EntryDto(DateOnly Date, TimeOnly Start, TimeOnly End, bool EndsNextDay, int BreakMinutes,
        bool WorkedFromHome, string? Notes, string? ProjectCode, decimal DecimalHours, string HoursMinutes);
}

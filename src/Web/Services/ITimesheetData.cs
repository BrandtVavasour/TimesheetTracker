using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// User-scoped data access for the UI. Every method is implicitly scoped to the
/// current user (via the DbContext query filter + explicit ownership checks).
/// </summary>
public interface ITimesheetData
{
    /// <summary>Demo "today" (within the seeded sample week). Replaced by a real clock later.</summary>
    DateOnly Today { get; }

    Task<AppUser> CurrentUserAsync();
    Task<IReadOnlyList<Job>> ActiveJobsAsync();
    Task<IReadOnlyList<Job>> AllJobsAsync();
    Task<Job?> JobAsync(Guid id);

    /// <summary>Entries for a job across an inclusive date range, with project codes loaded.</summary>
    Task<IReadOnlyList<TimeEntry>> EntriesAsync(Guid jobId, DateOnly from, DateOnly to);

    /// <summary>Every entry for a job (no date filter), with project codes loaded — for a full export.</summary>
    Task<IReadOnlyList<TimeEntry>> AllEntriesAsync(Guid jobId);

    Task SaveEntryAsync(Guid jobId, TimeEntry entry);
    Task DeleteEntryAsync(Guid jobId, Guid entryId);

    Task SaveJobAsync(Job job);
    Task<Job> AddJobAsync();

    /// <summary>Number of time entries on a job (owner-scoped) — for the delete confirmation.</summary>
    Task<int> EntryCountAsync(Guid jobId);

    /// <summary>Permanently delete a job and all its entries, custom fields and project
    /// codes. Owner-scoped: deleting a job you don't own is a silent no-op. Irreversible.</summary>
    Task DeleteJobAsync(Guid jobId);

    Task UpdateUserAsync(string displayName, AustralianState defaultState, bool exportIncludeAllDays);

    /// <summary>Effective holiday state for a job (override, else the user's default).</summary>
    AustralianState EffectiveState(Job job, AppUser user);

    /// <summary>Permanently delete the current user and ALL of their data
    /// (jobs, entries, custom fields, project codes). Irreversible.</summary>
    Task DeleteAccountAsync();
}

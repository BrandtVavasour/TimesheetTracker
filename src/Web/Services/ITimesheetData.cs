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

    Task SaveEntryAsync(Guid jobId, TimeEntry entry);
    Task DeleteEntryAsync(Guid jobId, Guid entryId);

    Task SaveJobAsync(Job job);
    Task<Job> AddJobAsync();

    Task UpdateUserAsync(string displayName, AustralianState defaultState);

    /// <summary>Effective holiday state for a job (override, else the user's default).</summary>
    AustralianState EffectiveState(Job job, AppUser user);
}

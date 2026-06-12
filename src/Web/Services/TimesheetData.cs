using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.Web.Services;

/// <summary>EF Core-backed implementation of <see cref="ITimesheetData"/>, scoped to the signed-in user.</summary>
public sealed class TimesheetData(IDbContextFactory<TimesheetDbContext> dbFactory, ICurrentUser user, IClock clock) : ITimesheetData
{
    public DateOnly Today => clock.Today;

    /// <summary>
    /// Create a fresh context for a single unit of work with the per-user query
    /// filters activated. A new context per call means concurrent operations on a
    /// Blazor Server circuit never collide ("A second operation was started...").
    /// </summary>
    private async Task<TimesheetDbContext> ScopeAsync()
    {
        var db = await dbFactory.CreateDbContextAsync();
        db.CurrentUserId = await user.GetIdAsync() ?? Guid.Empty;
        return db;
    }

    public async Task<AppUser> CurrentUserAsync()
    {
        await using var db = await ScopeAsync();
        return await db.Users.AsNoTracking().FirstAsync(u => u.Id == db.CurrentUserId);
    }

    public async Task<IReadOnlyList<Job>> ActiveJobsAsync()
    {
        await using var db = await ScopeAsync();
        return await db.Jobs.AsNoTracking()
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .Where(j => !j.IsArchived)
            .OrderBy(j => j.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Job>> AllJobsAsync()
    {
        await using var db = await ScopeAsync();
        return await db.Jobs.AsNoTracking()
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .OrderBy(j => j.DisplayOrder)
            .ToListAsync();
    }

    public async Task<Job?> JobAsync(Guid id)
    {
        await using var db = await ScopeAsync();
        return await db.Jobs.AsNoTracking()
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<IReadOnlyList<TimeEntry>> EntriesAsync(Guid jobId, DateOnly from, DateOnly to)
    {
        await using var db = await ScopeAsync();
        return await db.TimeEntries.AsNoTracking()
            .Include(e => e.ProjectCode)
            .Where(e => e.JobId == jobId && e.WorkDate >= from && e.WorkDate <= to)
            .OrderBy(e => e.WorkDate).ThenBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TimeEntry>> AllEntriesAsync(Guid jobId)
    {
        await using var db = await ScopeAsync();
        return await db.TimeEntries.AsNoTracking()
            .Include(e => e.ProjectCode)
            .Where(e => e.JobId == jobId)
            .OrderBy(e => e.WorkDate).ThenBy(e => e.StartTime)
            .ToListAsync();
    }

    public async Task SaveEntryAsync(Guid jobId, TimeEntry entry)
    {
        await using var db = await ScopeAsync();
        // Ownership is enforced by the query filter: a non-owned job is invisible.
        if (!await db.Jobs.AnyAsync(j => j.Id == jobId)) return;

        var existing = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == entry.Id);
        if (existing is null)
        {
            entry.JobId = jobId;
            entry.ProjectCode = null;
            entry.CreatedDate = DateTime.UtcNow;
            db.TimeEntries.Add(entry);
        }
        else
        {
            existing.StartTime = entry.StartTime;
            existing.EndTime = entry.EndTime;
            existing.EndsNextDay = entry.EndsNextDay;
            existing.BreakMinutes = entry.BreakMinutes;
            existing.IsWorkFromHome = entry.IsWorkFromHome;
            existing.Notes = entry.Notes;
            existing.ProjectCodeId = entry.ProjectCodeId;
            existing.ModifiedDate = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteEntryAsync(Guid jobId, Guid entryId)
    {
        await using var db = await ScopeAsync();
        var existing = await db.TimeEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.JobId == jobId);
        if (existing is null) return;
        db.TimeEntries.Remove(existing);
        await db.SaveChangesAsync();
    }

    public async Task SaveJobAsync(Job job)
    {
        await using var db = await ScopeAsync();
        var existing = await db.Jobs
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .FirstOrDefaultAsync(j => j.Id == job.Id);
        if (existing is null) return;

        existing.Name = job.Name;
        existing.StateOverride = job.StateOverride;
        existing.DecimalPlaces = job.DecimalPlaces;
        existing.WorkDays = job.WorkDays;
        existing.DefaultStartTime = job.DefaultStartTime;
        existing.DefaultEndTime = job.DefaultEndTime;
        existing.IsArchived = job.IsArchived;
        existing.ModifiedDate = DateTime.UtcNow;

        SyncCustomFields(db, existing, job.CustomFields);
        SyncProjectCodes(db, existing, job.ProjectCodes);

        await db.SaveChangesAsync();
    }

    public async Task<Job> AddJobAsync()
    {
        await using var db = await ScopeAsync();
        var maxOrder = await db.Jobs.MaxAsync(j => (int?)j.DisplayOrder) ?? -1;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            UserId = db.CurrentUserId,
            Name = "New job",
            DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Weekdays,
            DisplayOrder = maxOrder + 1,
            CreatedDate = DateTime.UtcNow,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    public async Task UpdateUserAsync(string displayName, AustralianState defaultState, bool exportIncludeAllDays)
    {
        await using var db = await ScopeAsync();
        var appUser = await db.Users.FirstAsync(u => u.Id == db.CurrentUserId);
        appUser.DisplayName = displayName;
        appUser.DefaultState = defaultState;
        appUser.ExportIncludeAllDays = exportIncludeAllDays;
        await db.SaveChangesAsync();
    }

    public AustralianState EffectiveState(Job job, AppUser appUser) => job.StateOverride ?? appUser.DefaultState;

    public async Task DeleteAccountAsync()
    {
        var userId = await user.GetIdAsync();
        if (userId is null) return;

        await using var db = await dbFactory.CreateDbContextAsync();
        db.CurrentUserId = userId.Value;

        // Load each job with its dependents so EF cascades them (InMemory has no
        // FK cascade; Postgres also cascades at the DB). Then remove the user.
        var jobs = await db.Jobs
            .Include(j => j.TimeEntries)
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .ToListAsync();
        db.Jobs.RemoveRange(jobs);

        var appUser = await db.Users.FirstAsync(u => u.Id == userId.Value);
        db.Users.Remove(appUser);

        await db.SaveChangesAsync();
    }

    // New children are added via the DbSet, not just the parent's collection:
    // their ids are page-generated (already set), and EF's graph discovery treats
    // a discovered entity with a set generated key as existing → Modified → an
    // UPDATE for a row that was never inserted (DbUpdateConcurrencyException).
    // DbSet.Add forces Added; navigation fixup attaches it to the collection.

    private static void SyncCustomFields(TimesheetDbContext db, Job existing, ICollection<JobCustomField> incoming)
    {
        foreach (var stale in existing.CustomFields.Where(f => incoming.All(i => i.Id != f.Id)).ToList())
            existing.CustomFields.Remove(stale);
        foreach (var inc in incoming)
        {
            var cur = existing.CustomFields.FirstOrDefault(f => f.Id == inc.Id);
            if (cur is null)
            {
                db.JobCustomFields.Add(new()
                {
                    Id = inc.Id == Guid.Empty ? Guid.NewGuid() : inc.Id,
                    JobId = existing.Id, Name = inc.Name, Value = inc.Value,
                    ShowOnTimesheet = inc.ShowOnTimesheet, DisplayOrder = inc.DisplayOrder,
                });
            }
            else
            {
                cur.Name = inc.Name; cur.Value = inc.Value;
                cur.ShowOnTimesheet = inc.ShowOnTimesheet; cur.DisplayOrder = inc.DisplayOrder;
            }
        }
    }

    private static void SyncProjectCodes(TimesheetDbContext db, Job existing, ICollection<ProjectCode> incoming)
    {
        foreach (var stale in existing.ProjectCodes.Where(p => incoming.All(i => i.Id != p.Id)).ToList())
            existing.ProjectCodes.Remove(stale);
        foreach (var inc in incoming)
        {
            var cur = existing.ProjectCodes.FirstOrDefault(p => p.Id == inc.Id);
            if (cur is null)
            {
                db.ProjectCodes.Add(new()
                {
                    Id = inc.Id == Guid.Empty ? Guid.NewGuid() : inc.Id,
                    JobId = existing.Id, Code = inc.Code, Description = inc.Description,
                    IsActive = inc.IsActive, DisplayOrder = inc.DisplayOrder,
                });
            }
            else
            {
                cur.Code = inc.Code; cur.Description = inc.Description;
                cur.IsActive = inc.IsActive; cur.DisplayOrder = inc.DisplayOrder;
            }
        }
    }
}

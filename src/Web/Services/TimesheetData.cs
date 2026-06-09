using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.Web.Services;

/// <summary>EF Core-backed implementation of <see cref="ITimesheetData"/>, scoped to the current user.</summary>
public sealed class TimesheetData : ITimesheetData
{
    private readonly TimesheetDbContext _db;
    private readonly ICurrentUser _user;

    public TimesheetData(TimesheetDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
        _db.CurrentUserId = user.Id; // activates the per-user query filters
    }

    public DateOnly Today => SeedData.Today;

    public async Task<AppUser> CurrentUserAsync() =>
        await _db.Users.AsNoTracking().FirstAsync(u => u.Id == _user.Id);

    public async Task<IReadOnlyList<Job>> ActiveJobsAsync() =>
        await _db.Jobs.AsNoTracking()
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .Where(j => !j.IsArchived)
            .OrderBy(j => j.DisplayOrder)
            .ToListAsync();

    public async Task<IReadOnlyList<Job>> AllJobsAsync() =>
        await _db.Jobs.AsNoTracking()
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .OrderBy(j => j.DisplayOrder)
            .ToListAsync();

    public async Task<Job?> JobAsync(Guid id) =>
        await _db.Jobs.AsNoTracking()
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<IReadOnlyList<TimeEntry>> EntriesAsync(Guid jobId, DateOnly from, DateOnly to) =>
        await _db.TimeEntries.AsNoTracking()
            .Include(e => e.ProjectCode)
            .Where(e => e.JobId == jobId && e.WorkDate >= from && e.WorkDate <= to)
            .OrderBy(e => e.WorkDate).ThenBy(e => e.StartTime)
            .ToListAsync();

    public async Task SaveEntryAsync(Guid jobId, TimeEntry entry)
    {
        // Ownership is enforced by the query filter: a non-owned job returns null.
        if (!await _db.Jobs.AnyAsync(j => j.Id == jobId)) return;

        var existing = await _db.TimeEntries.FirstOrDefaultAsync(e => e.Id == entry.Id);
        if (existing is null)
        {
            entry.JobId = jobId;
            entry.ProjectCode = null;
            entry.CreatedDate = DateTime.UtcNow;
            _db.TimeEntries.Add(entry);
        }
        else
        {
            existing.StartTime = entry.StartTime;
            existing.EndTime = entry.EndTime;
            existing.EndsNextDay = entry.EndsNextDay;
            existing.BreakMinutes = entry.BreakMinutes;
            existing.Notes = entry.Notes;
            existing.ProjectCodeId = entry.ProjectCodeId;
            existing.ModifiedDate = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteEntryAsync(Guid jobId, Guid entryId)
    {
        var existing = await _db.TimeEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.JobId == jobId);
        if (existing is null) return;
        _db.TimeEntries.Remove(existing);
        await _db.SaveChangesAsync();
    }

    public async Task SaveJobAsync(Job job)
    {
        var existing = await _db.Jobs
            .Include(j => j.CustomFields)
            .Include(j => j.ProjectCodes)
            .FirstOrDefaultAsync(j => j.Id == job.Id);
        if (existing is null) return;

        existing.Name = job.Name;
        existing.StateOverride = job.StateOverride;
        existing.DecimalPlaces = job.DecimalPlaces;
        existing.WorkDays = job.WorkDays;
        existing.IsArchived = job.IsArchived;
        existing.ModifiedDate = DateTime.UtcNow;

        SyncCustomFields(existing, job.CustomFields);
        SyncProjectCodes(existing, job.ProjectCodes);

        await _db.SaveChangesAsync();
    }

    public async Task<Job> AddJobAsync()
    {
        var maxOrder = await _db.Jobs.MaxAsync(j => (int?)j.DisplayOrder) ?? -1;
        var job = new Job
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            Name = "New job",
            DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Weekdays,
            DisplayOrder = maxOrder + 1,
            CreatedDate = DateTime.UtcNow,
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();
        return job;
    }

    public async Task UpdateUserAsync(string displayName, AustralianState defaultState)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == _user.Id);
        user.DisplayName = displayName;
        user.DefaultState = defaultState;
        await _db.SaveChangesAsync();
    }

    public AustralianState EffectiveState(Job job, AppUser user) => job.StateOverride ?? user.DefaultState;

    private static void SyncCustomFields(Job existing, ICollection<JobCustomField> incoming)
    {
        foreach (var stale in existing.CustomFields.Where(f => incoming.All(i => i.Id != f.Id)).ToList())
            existing.CustomFields.Remove(stale);
        foreach (var inc in incoming)
        {
            var cur = existing.CustomFields.FirstOrDefault(f => f.Id == inc.Id);
            if (cur is null)
            {
                existing.CustomFields.Add(new JobCustomField
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

    private static void SyncProjectCodes(Job existing, ICollection<ProjectCode> incoming)
    {
        foreach (var stale in existing.ProjectCodes.Where(p => incoming.All(i => i.Id != p.Id)).ToList())
            existing.ProjectCodes.Remove(stale);
        foreach (var inc in incoming)
        {
            var cur = existing.ProjectCodes.FirstOrDefault(p => p.Id == inc.Id);
            if (cur is null)
            {
                existing.ProjectCodes.Add(new ProjectCode
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

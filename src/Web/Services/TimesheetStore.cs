using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.Web.Services;

/// <summary>
/// In-memory, demo data store seeded with the Claude Design sample week.
/// Holds the real domain entities (Job/TimeEntry/...) so components reuse the
/// tested calculation + holiday services. This is a development/demo stand-in
/// for the EF Core + Identity persistence layer (a follow-up task).
/// </summary>
public class TimesheetStore
{
    public AppUser User { get; }
    private readonly List<Job> _jobs;

    /// <summary>The seed "today" — within the sample week so highlighting reads naturally.</summary>
    public DateOnly Today { get; } = new(2026, 6, 11);

    public TimesheetStore()
    {
        User = new AppUser
        {
            DisplayName = "Alex Carter",
            UserName = "alex@example.com",
            Email = "alex@example.com",
            DefaultState = AustralianState.NSW
        };
        _jobs = SeedJobs();
    }

    public IReadOnlyList<Job> AllJobs => _jobs.OrderBy(j => j.DisplayOrder).ToList();
    public IReadOnlyList<Job> ActiveJobs => _jobs.Where(j => !j.IsArchived).OrderBy(j => j.DisplayOrder).ToList();
    public Job GetJob(Guid id) => _jobs.FirstOrDefault(j => j.Id == id) ?? ActiveJobs[0];

    public AustralianState EffectiveState(Job job) => job.StateOverride ?? User.DefaultState;

    public IReadOnlyList<TimeEntry> EntriesFor(Guid jobId, DateOnly date) =>
        GetJob(jobId).TimeEntries
            .Where(e => e.WorkDate == date)
            .OrderBy(e => e.StartTime)
            .ToList();

    public void Save(Guid jobId, TimeEntry entry)
    {
        var job = GetJob(jobId);
        var existing = job.TimeEntries.FirstOrDefault(e => e.Id == entry.Id);
        if (existing is null)
        {
            entry.JobId = jobId;
            entry.ProjectCode = entry.ProjectCodeId is { } pid ? job.ProjectCodes.FirstOrDefault(c => c.Id == pid) : null;
            job.TimeEntries.Add(entry);
        }
        else
        {
            existing.StartTime = entry.StartTime;
            existing.EndTime = entry.EndTime;
            existing.EndsNextDay = entry.EndsNextDay;
            existing.BreakMinutes = entry.BreakMinutes;
            existing.Notes = entry.Notes;
            existing.ProjectCodeId = entry.ProjectCodeId;
            existing.ProjectCode = entry.ProjectCodeId is { } pid ? job.ProjectCodes.FirstOrDefault(c => c.Id == pid) : null;
        }
    }

    public void Delete(Guid jobId, Guid entryId)
    {
        var job = GetJob(jobId);
        var existing = job.TimeEntries.FirstOrDefault(e => e.Id == entryId);
        if (existing is not null) job.TimeEntries.Remove(existing);
    }

    private static List<Job> SeedJobs()
    {
        var acme = new Job
        {
            Id = Guid.NewGuid(), Name = "Acme Corp", StateOverride = null, DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Weekdays, IsArchived = false, DisplayOrder = 0,
            CustomFields =
            [
                new() { Id = Guid.NewGuid(), Name = "Employee #", Value = "40192", ShowOnTimesheet = true, DisplayOrder = 0 },
                new() { Id = Guid.NewGuid(), Name = "Cost centre", Value = "OPS-200", ShowOnTimesheet = true, DisplayOrder = 1 },
                new() { Id = Guid.NewGuid(), Name = "Manager", Value = "R. Tan", ShowOnTimesheet = false, DisplayOrder = 2 },
            ],
        };
        var acmePrj1234 = new ProjectCode { Id = Guid.NewGuid(), Code = "PRJ-1234", Description = "Website rebuild", IsActive = true, DisplayOrder = 0 };
        var acmePrj2087 = new ProjectCode { Id = Guid.NewGuid(), Code = "PRJ-2087", Description = "Platform migration", IsActive = true, DisplayOrder = 1 };
        var acmeIntOps = new ProjectCode { Id = Guid.NewGuid(), Code = "INT-OPS", Description = "Internal operations", IsActive = true, DisplayOrder = 2 };
        var acmeLegacy = new ProjectCode { Id = Guid.NewGuid(), Code = "PRJ-0098", Description = "Legacy maintenance", IsActive = false, DisplayOrder = 3 };
        acme.ProjectCodes = [acmePrj1234, acmePrj2087, acmeIntOps, acmeLegacy];
        acme.TimeEntries =
        [
            Entry(acme, new(2026, 6, 9), "09:00", "17:30", false, 30, "Sprint planning + feature dev", acmePrj1234),
            Entry(acme, new(2026, 6, 10), "08:30", "16:00", false, 30, "Checkout flow build", acmePrj1234),
            Entry(acme, new(2026, 6, 10), "19:00", "21:00", false, 0, "On-call: prod deploy", acmeIntOps),
            Entry(acme, new(2026, 6, 11), "09:00", "17:00", false, 45, "Data migration dry-run", acmePrj2087),
            Entry(acme, new(2026, 6, 12), "22:00", "02:30", true, 30, "Overnight cutover window", acmeIntOps),
        ];

        var cafe = new Job
        {
            Id = Guid.NewGuid(), Name = "Northwind Café", StateOverride = AustralianState.VIC, DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Wednesday | DaysOfWeek.Thursday | DaysOfWeek.Friday | DaysOfWeek.Saturday | DaysOfWeek.Sunday,
            IsArchived = false, DisplayOrder = 1,
            CustomFields = [new() { Id = Guid.NewGuid(), Name = "Roster ID", Value = "NW-CAS-07", ShowOnTimesheet = true, DisplayOrder = 0 }],
            ProjectCodes = [],
        };
        cafe.TimeEntries =
        [
            Entry(cafe, new(2026, 6, 11), "17:00", "22:00", false, 30, "Dinner service", null),
            Entry(cafe, new(2026, 6, 13), "11:00", "19:30", false, 45, "Lunch + dinner double", null),
        ];

        var studio = new Job
        {
            Id = Guid.NewGuid(), Name = "Studio Bright (freelance)", StateOverride = null, DecimalPlaces = 3,
            WorkDays = DaysOfWeek.Weekdays, IsArchived = false, DisplayOrder = 2,
            CustomFields = [new() { Id = Guid.NewGuid(), Name = "ABN", Value = "54 882 011 230", ShowOnTimesheet = false, DisplayOrder = 0 }],
        };
        var studioBrand = new ProjectCode { Id = Guid.NewGuid(), Code = "BRAND-22", Description = "Brand refresh", IsActive = true, DisplayOrder = 0 };
        var studioWeb05 = new ProjectCode { Id = Guid.NewGuid(), Code = "WEB-05", Description = "Marketing site", IsActive = true, DisplayOrder = 1 };
        studio.ProjectCodes = [studioBrand, studioWeb05];
        studio.TimeEntries =
        [
            Entry(studio, new(2026, 6, 9), "13:00", "16:45", false, 0, "Logo concept round 1", studioBrand),
            Entry(studio, new(2026, 6, 11), "10:00", "12:20", false, 0, "Marketing site wireframes", studioWeb05),
        ];

        var oldgig = new Job
        {
            Id = Guid.NewGuid(), Name = "Bayside Logistics", StateOverride = null, DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Weekdays, IsArchived = true, DisplayOrder = 3,
            CustomFields = [], ProjectCodes = [],
        };

        return [acme, cafe, studio, oldgig];
    }

    private static TimeEntry Entry(Job job, DateOnly date, string start, string end, bool endsNextDay, int breakMin, string notes, ProjectCode? code) =>
        new()
        {
            Id = Guid.NewGuid(),
            Job = job,
            WorkDate = date,
            StartTime = TimeOnly.Parse(start),
            EndTime = TimeOnly.Parse(end),
            EndsNextDay = endsNextDay,
            BreakMinutes = breakMin,
            Notes = notes,
            ProjectCodeId = code?.Id,
            ProjectCode = code
        };
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel;

/// <summary>
/// Canonical sample/test data (the Claude Design "sample week"). Used to seed the
/// development database and to provide a known fixture for tests. Mirrors the design
/// prototype: a King's Birthday holiday (Mon 8 Jun), an overnight cutover crossing
/// midnight, a two-entry day, and a 3-decimal-place freelance job.
/// </summary>
public static class SeedData
{
    public static readonly Guid DemoUserId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>The seed "today" — within the sample week so highlighting reads naturally.</summary>
    public static readonly DateOnly Today = new(2026, 6, 11);

    /// <summary>The seeded demo account's dev password (Development only) — lets you sign in to see the sample week.</summary>
    public const string DemoPassword = "TimesheetDev123!";

    /// <summary>Idempotently seed the database if it has no users.</summary>
    public static async Task SeedAsync(TimesheetDbContext db, CancellationToken cancel = default)
    {
        if (await db.Users.AnyAsync(cancel)) return;
        var demoUser = BuildUser();
        demoUser.PasswordHash = new PasswordHasher<AppUser>().HashPassword(demoUser, DemoPassword);
        db.Users.Add(demoUser);
        db.Jobs.AddRange(BuildJobs(DemoUserId));
        await db.SaveChangesAsync(cancel);
    }

    public static AppUser BuildUser() => new()
    {
        Id = DemoUserId,
        DisplayName = "Alex Carter",
        UserName = "alex@example.com",
        NormalizedUserName = "ALEX@EXAMPLE.COM",
        Email = "alex@example.com",
        NormalizedEmail = "ALEX@EXAMPLE.COM",
        EmailConfirmed = true,
        DefaultState = AustralianState.NSW,
        SecurityStamp = Guid.NewGuid().ToString()
    };

    public static List<Job> BuildJobs(Guid userId)
    {
        var acme = new Job
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Acme Corp", StateOverride = null, DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Weekdays, IsArchived = false, DisplayOrder = 0,
            CustomFields =
            [
                new() { Id = Guid.NewGuid(), Name = "Employee #", Value = "40192", ShowOnTimesheet = true, DisplayOrder = 0 },
                new() { Id = Guid.NewGuid(), Name = "Cost centre", Value = "OPS-200", ShowOnTimesheet = true, DisplayOrder = 1 },
                new() { Id = Guid.NewGuid(), Name = "Manager", Value = "R. Tan", ShowOnTimesheet = false, DisplayOrder = 2 },
            ],
        };
        var prj1234 = new ProjectCode { Id = Guid.NewGuid(), Code = "PRJ-1234", Description = "Website rebuild", IsActive = true, DisplayOrder = 0 };
        var prj2087 = new ProjectCode { Id = Guid.NewGuid(), Code = "PRJ-2087", Description = "Platform migration", IsActive = true, DisplayOrder = 1 };
        var intOps = new ProjectCode { Id = Guid.NewGuid(), Code = "INT-OPS", Description = "Internal operations", IsActive = true, DisplayOrder = 2 };
        var legacy = new ProjectCode { Id = Guid.NewGuid(), Code = "PRJ-0098", Description = "Legacy maintenance", IsActive = false, DisplayOrder = 3 };
        acme.ProjectCodes = [prj1234, prj2087, intOps, legacy];
        acme.TimeEntries =
        [
            Entry(new(2026, 6, 9), "09:00", "17:30", false, 30, "Sprint planning + feature dev", prj1234),
            Entry(new(2026, 6, 10), "08:30", "16:00", false, 30, "Checkout flow build", prj1234, wfh: true),
            Entry(new(2026, 6, 10), "19:00", "21:00", false, 0, "On-call: prod deploy", intOps),
            Entry(new(2026, 6, 11), "09:00", "17:00", false, 45, "Data migration dry-run", prj2087),
            Entry(new(2026, 6, 12), "22:00", "02:30", true, 30, "Overnight cutover window", intOps),
        ];

        var cafe = new Job
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Northwind Café", StateOverride = AustralianState.VIC, DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Wednesday | DaysOfWeek.Thursday | DaysOfWeek.Friday | DaysOfWeek.Saturday | DaysOfWeek.Sunday,
            IsArchived = false, DisplayOrder = 1,
            CustomFields = [new() { Id = Guid.NewGuid(), Name = "Roster ID", Value = "NW-CAS-07", ShowOnTimesheet = true, DisplayOrder = 0 }],
            ProjectCodes = [],
        };
        cafe.TimeEntries =
        [
            Entry(new(2026, 6, 11), "17:00", "22:00", false, 30, "Dinner service", null),
            Entry(new(2026, 6, 13), "11:00", "19:30", false, 45, "Lunch + dinner double", null),
        ];

        var studio = new Job
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Studio Bright (freelance)", StateOverride = null, DecimalPlaces = 3,
            WorkDays = DaysOfWeek.Weekdays, IsArchived = false, DisplayOrder = 2,
            CustomFields = [new() { Id = Guid.NewGuid(), Name = "ABN", Value = "54 882 011 230", ShowOnTimesheet = false, DisplayOrder = 0 }],
        };
        var brand = new ProjectCode { Id = Guid.NewGuid(), Code = "BRAND-22", Description = "Brand refresh", IsActive = true, DisplayOrder = 0 };
        var web05 = new ProjectCode { Id = Guid.NewGuid(), Code = "WEB-05", Description = "Marketing site", IsActive = true, DisplayOrder = 1 };
        studio.ProjectCodes = [brand, web05];
        studio.TimeEntries =
        [
            Entry(new(2026, 6, 9), "13:00", "16:45", false, 0, "Logo concept round 1", brand),
            Entry(new(2026, 6, 11), "10:00", "12:20", false, 0, "Marketing site wireframes", web05),
        ];

        var oldgig = new Job
        {
            Id = Guid.NewGuid(), UserId = userId, Name = "Bayside Logistics", StateOverride = null, DecimalPlaces = 2,
            WorkDays = DaysOfWeek.Weekdays, IsArchived = true, DisplayOrder = 3,
            CustomFields = [], ProjectCodes = [],
        };

        return [acme, cafe, studio, oldgig];
    }

    private static TimeEntry Entry(DateOnly date, string start, string end, bool endsNextDay, int breakMin, string notes, ProjectCode? code, bool wfh = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkDate = date,
            StartTime = TimeOnly.Parse(start),
            EndTime = TimeOnly.Parse(end),
            EndsNextDay = endsNextDay,
            BreakMinutes = breakMin,
            IsWorkFromHome = wfh,
            Notes = notes,
            ProjectCode = code,
            ProjectCodeId = code?.Id
        };
}

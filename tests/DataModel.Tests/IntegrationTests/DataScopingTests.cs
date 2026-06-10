using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;

namespace DataModel.Tests.IntegrationTests;

/// <summary>
/// Security tests for the per-user query filters: a user must never see another
/// user's jobs or entries, even querying the DbSet directly (anti-IDOR).
/// Uses the EF InMemory provider — no Docker required.
/// </summary>
[TestFixture]
public class DataScopingTests
{
    private static TimesheetDbContext NewContext(string dbName) =>
        new(new DbContextOptionsBuilder<TimesheetDbContext>().UseInMemoryDatabase(dbName).Options);

    [Test]
    public async Task QueryFilter_ScopesJobsToCurrentUser()
    {
        var db = NewContext(nameof(QueryFilter_ScopesJobsToCurrentUser));
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        db.Jobs.Add(new()
            { Id = Guid.NewGuid(), UserId = userA, Name = "A's job" });
        db.Jobs.Add(new()
            { Id = Guid.NewGuid(), UserId = userB, Name = "B's job" });
        await db.SaveChangesAsync();

        db.CurrentUserId = userA;
        var aJobs = await db.Jobs.ToListAsync();
        aJobs.Should().ContainSingle().Which.Name.Should().Be("A's job");

        db.CurrentUserId = userB;
        var bJobs = await db.Jobs.ToListAsync();
        bJobs.Should().ContainSingle().Which.Name.Should().Be("B's job");
    }

    [Test]
    public async Task QueryFilter_ScopesTimeEntriesToCurrentUser()
    {
        var db = NewContext(nameof(QueryFilter_ScopesTimeEntriesToCurrentUser));
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var jobA = new Job { Id = Guid.NewGuid(), UserId = userA, Name = "A" };
        var jobB = new Job { Id = Guid.NewGuid(), UserId = userB, Name = "B" };
        jobA.TimeEntries.Add(Entry());
        jobB.TimeEntries.Add(Entry());
        jobB.TimeEntries.Add(Entry());
        db.Jobs.AddRange(jobA, jobB);
        await db.SaveChangesAsync();

        db.CurrentUserId = userA;
        (await db.TimeEntries.CountAsync()).Should().Be(1);

        db.CurrentUserId = userB;
        (await db.TimeEntries.CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task Seed_PopulatesSampleData()
    {
        var db = NewContext(nameof(Seed_PopulatesSampleData));
        await SeedData.SeedAsync(db);

        db.CurrentUserId = SeedData.DemoUserId;
        var jobs = await db.Jobs.Include(j => j.TimeEntries).Include(j => j.ProjectCodes).ToListAsync();
        jobs.Should().HaveCount(4); // Acme, Café, Studio, archived Bayside
        jobs.Single(j => j.Name == "Acme Corp").TimeEntries.Should().HaveCount(5);
        jobs.Single(j => j.Name == "Acme Corp").ProjectCodes.Should().HaveCount(4);
    }

    [Test]
    public async Task Seed_IsIdempotent()
    {
        var db = NewContext(nameof(Seed_IsIdempotent));
        await SeedData.SeedAsync(db);
        await SeedData.SeedAsync(db);

        db.CurrentUserId = SeedData.DemoUserId;
        (await db.Jobs.CountAsync()).Should().Be(4);
    }

    private static TimeEntry Entry() => new()
    {
        Id = Guid.NewGuid(),
        WorkDate = new(2026, 6, 9),
        StartTime = new(9, 0),
        EndTime = new(17, 0),
        BreakMinutes = 30,
    };
}

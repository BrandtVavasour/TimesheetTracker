using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Deleting a job removes the job and all its entries; the entry count is accurate;
/// and a user can NEVER delete (or count the entries of) a job they don't own.
/// </summary>
[TestFixture]
public class JobDeletionTests
{
    private DbContextOptions<TimesheetDbContext> options = null!;

    [SetUp]
    public void SetUp() => options = new DbContextOptionsBuilder<TimesheetDbContext>()
        .UseInMemoryDatabase("jobdel-" + Guid.NewGuid()).Options;

    private TimesheetData DataFor(Guid id) => new(new Factory(options), new Stub(id), new Clock());

    private async Task<Guid> CreateUser(string email)
    {
        var id = Guid.NewGuid();
        await using var db = new TimesheetDbContext(options);
        db.Users.Add(new()
            { Id = id, UserName = email, Email = email, DisplayName = email, DefaultState = AustralianState.NSW });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> AddJobWithEntries(TimesheetData data, int entries)
    {
        var job = await data.AddJobAsync();
        for (var i = 0; i < entries; i++)
        {
            await data.SaveEntryAsync(job.Id, new()
            {
                Id = Guid.NewGuid(), WorkDate = new DateOnly(2026, 6, 1).AddDays(i),
                StartTime = new(9, 0), EndTime = new(17, 0), BreakMinutes = 30,
            });
        }
        return job.Id;
    }

    [Test]
    public async Task DeleteJob_RemovesJobAndItsEntries_LeavesOtherJobsIntact()
    {
        var uid = await CreateUser("u@example.com");
        var data = DataFor(uid);
        var doomed = await AddJobWithEntries(data, 3);
        var keep = await AddJobWithEntries(data, 2);

        (await data.EntryCountAsync(doomed)).Should().Be(3);

        await data.DeleteJobAsync(doomed);

        (await data.JobAsync(doomed)).Should().BeNull();
        (await data.JobAsync(keep)).Should().NotBeNull();
        await using var db = new TimesheetDbContext(options);
        (await db.Jobs.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await db.TimeEntries.IgnoreQueryFilters().CountAsync()).Should().Be(2); // only "keep"'s entries remain
    }

    [Test]
    public async Task User_CannotDeleteOrCountAnotherUsersJob()
    {
        var owner = await CreateUser("owner@example.com");
        var attacker = await CreateUser("attacker@example.com");
        var ownerJob = await AddJobWithEntries(DataFor(owner), 4);

        var b = DataFor(attacker);
        (await b.EntryCountAsync(ownerJob)).Should().Be(0);   // can't even see the count
        await b.DeleteJobAsync(ownerJob);                     // silent no-op

        // The owner's job and entries are completely untouched.
        (await DataFor(owner).JobAsync(ownerJob)).Should().NotBeNull();
        await using var db = new TimesheetDbContext(options);
        (await db.TimeEntries.IgnoreQueryFilters().CountAsync(e => e.JobId == ownerJob)).Should().Be(4);
    }

    private sealed class Factory(DbContextOptions<TimesheetDbContext> o) : IDbContextFactory<TimesheetDbContext>
    {
        public TimesheetDbContext CreateDbContext() => new(o);
    }

    private sealed class Stub(Guid id) : ICurrentUser
    {
        public Task<Guid?> GetIdAsync() => Task.FromResult<Guid?>(id);
    }

    private sealed class Clock : IClock
    {
        public DateOnly Today => new(2026, 6, 12);
    }
}

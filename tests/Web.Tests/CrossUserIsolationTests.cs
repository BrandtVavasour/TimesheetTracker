using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// End-to-end proof that one user can NEVER read or mutate another user's jobs
/// or time entries through the real <see cref="TimesheetData"/> service — even
/// when supplying a valid id that belongs to the other user. Both users share
/// one database; isolation comes from the per-user EF query filters plus the
/// server-resolved CurrentUserId.
/// </summary>
[TestFixture]
public class CrossUserIsolationTests
{
    private DbContextOptions<TimesheetDbContext> _options = null!;
    private Guid _userA;
    private Guid _userB;

    [SetUp]
    public async Task SetUp()
    {
        _options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseInMemoryDatabase("xuser-" + Guid.NewGuid())
            .Options;
        _userA = await CreateUser("a@example.com");
        _userB = await CreateUser("b@example.com");
    }

    private async Task<Guid> CreateUser(string email)
    {
        var id = Guid.NewGuid();
        await using var db = new TimesheetDbContext(_options);
        db.Users.Add(new AppUser { Id = id, UserName = email, Email = email, DisplayName = email, DefaultState = AustralianState.NSW });
        await db.SaveChangesAsync();
        return id;
    }

    private TimesheetData DataFor(Guid userId) =>
        new(new Factory(_options), new Stub(userId), new Clock());

    [Test]
    public async Task UserB_CannotSeeOrTouch_UserAsJobAndEntries()
    {
        var a = DataFor(_userA);
        var b = DataFor(_userB);

        // User A sets up a job with an entry.
        var jobA = await a.AddJobAsync();
        var jobA_clone = await a.JobAsync(jobA.Id);
        jobA_clone!.Name = "A's secret client";
        await a.SaveJobAsync(jobA_clone);
        await a.SaveEntryAsync(jobA.Id, new TimeEntry
        {
            Id = Guid.NewGuid(), WorkDate = new(2026, 6, 10),
            StartTime = new(9, 0), EndTime = new(17, 0), BreakMinutes = 30, Notes = "A's private notes",
        });
        var aEntry = (await a.EntriesAsync(jobA.Id, new(2026, 6, 10), new(2026, 6, 10))).Single();

        // --- READ: B sees nothing of A's, even with A's exact ids ---
        (await b.AllJobsAsync()).Should().BeEmpty();
        (await b.ActiveJobsAsync()).Should().BeEmpty();
        (await b.JobAsync(jobA.Id)).Should().BeNull();
        (await b.EntriesAsync(jobA.Id, new(2026, 6, 1), new(2026, 6, 30))).Should().BeEmpty();

        // --- WRITE: B's attempts to mutate A's data are silent no-ops ---
        // Try to add an entry onto A's job.
        await b.SaveEntryAsync(jobA.Id, new TimeEntry
        {
            Id = Guid.NewGuid(), WorkDate = new(2026, 6, 11),
            StartTime = new(0, 0), EndTime = new(23, 0), BreakMinutes = 0, Notes = "B injected this",
        });
        // Try to rename A's job.
        await b.SaveJobAsync(new Job { Id = jobA.Id, Name = "B hijacked this" });
        // Try to delete A's entry.
        await b.DeleteEntryAsync(jobA.Id, aEntry.Id);

        // --- A's data is completely untouched ---
        var jobAfter = await a.JobAsync(jobA.Id);
        jobAfter!.Name.Should().Be("A's secret client");
        var entriesAfter = await a.EntriesAsync(jobA.Id, new(2026, 6, 1), new(2026, 6, 30));
        entriesAfter.Should().ContainSingle();
        entriesAfter[0].Id.Should().Be(aEntry.Id);
        entriesAfter[0].Notes.Should().Be("A's private notes");
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

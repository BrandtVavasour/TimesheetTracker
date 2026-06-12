using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Proves account deletion removes the user and ALL their data (jobs, entries,
/// custom fields, project codes) while leaving other users' data untouched.
/// </summary>
[TestFixture]
public class AccountDeletionTests
{
    private DbContextOptions<TimesheetDbContext> options = null!;

    [SetUp]
    public void SetUp() => options = new DbContextOptionsBuilder<TimesheetDbContext>()
        .UseInMemoryDatabase("del-" + Guid.NewGuid()).Options;

    private TimesheetData DataFor(Guid id) => new(new Factory(options), new Stub(id), new Clock());

    private async Task<Guid> CreateUserWithData(string email)
    {
        var id = Guid.NewGuid();
        await using (var db = new TimesheetDbContext(options))
        {
            db.Users.Add(new()
                { Id = id, UserName = email, Email = email, DisplayName = email, DefaultState = AustralianState.NSW });
            await db.SaveChangesAsync();
        }

        var data = DataFor(id);
        var job = await data.AddJobAsync();
        var clone = await data.JobAsync(job.Id);
        clone!.Name = "Client";
        clone.CustomFields.Add(new() { Id = Guid.NewGuid(), Name = "Emp #", Value = "1", ShowOnTimesheet = true });
        clone.ProjectCodes.Add(new() { Id = Guid.NewGuid(), Code = "PRJ-1", IsActive = true });
        await data.SaveJobAsync(clone);
        await data.SaveEntryAsync(job.Id, new()
            { Id = Guid.NewGuid(), WorkDate = new(2026, 6, 10), StartTime = new(9, 0), EndTime = new(17, 0), BreakMinutes = 30 });
        return id;
    }

    [Test]
    public async Task DeleteAccount_RemovesUserAndAllData_LeavesOtherUsersIntact()
    {
        var u1 = await CreateUserWithData("u1@example.com");
        var u2 = await CreateUserWithData("u2@example.com");

        await DataFor(u1).DeleteAccountAsync();

        await using var db = new TimesheetDbContext(options);
        // Only user 2 (and exactly their one-of-each) remains.
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.Users.SingleAsync()).Id.Should().Be(u2);
        (await db.Jobs.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await db.TimeEntries.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await db.JobCustomFields.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await db.ProjectCodes.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await db.Jobs.IgnoreQueryFilters().SingleAsync()).UserId.Should().Be(u2);
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

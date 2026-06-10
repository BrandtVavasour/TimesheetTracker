using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel.Enums;

namespace DataModel.Tests.IntegrationTests;

[TestFixture]
[Explicit("Requires Docker")]
[Category("Docker")]
public class JobPersistenceTests : TestContainerBase
{
    [Test]
    public async Task Job_WithEntries_RoundTrips()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "a@b.com",
            DisplayName = "A",
            DefaultState = AustralianState.VIC
        };
        var job = new Job { Id = Guid.NewGuid(), Name = "Acme", User = user, DecimalPlaces = 2 };
        job.TimeEntries.Add(new()
        {
            Id = Guid.NewGuid(),
            WorkDate = new(2026, 6, 8),
            StartTime = new(9, 0),
            EndTime = new(17, 0),
            BreakMinutes = 30
        });
        Db.Add(job);
        await Db.SaveChangesAsync();

        var loaded = await Db.Jobs.Include(j => j.TimeEntries).SingleAsync();
        loaded.TimeEntries.Should().HaveCount(1);
        loaded.WorkDays.Should().Be(DaysOfWeek.Weekdays);
    }
}

using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;

namespace DataModel.Tests.IntegrationTests;

[TestFixture]
public class DbContextModelTests
{
    [Test]
    public Task Model_MatchesSnapshot()
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseNpgsql("Host=localhost;Database=x;Username=x;Password=x")
            .Options;
        using var context = new TimesheetDbContext(options);
        return Verify(context.Model);
    }
}

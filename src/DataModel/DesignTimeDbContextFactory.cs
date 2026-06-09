using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TimesheetTracker.DataModel;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TimesheetDbContext>
{
    public TimesheetDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseNpgsql("Host=localhost;Database=timesheet;Username=postgres;Password=postgres")
            .Options;
        return new TimesheetDbContext(options);
    }
}

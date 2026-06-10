using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TimesheetTracker.DataModel;

namespace DataModel.Tests.IntegrationTests;

/// <summary>
/// Base for integration tests that exercise a real Postgres instance via Testcontainers.
/// Marked [Explicit] because it requires a running Docker daemon; run with
/// <c>dotnet test --filter "TestCategory=Docker"</c> or select explicitly.
/// </summary>
[Explicit("Requires Docker")]
[Category("Docker")]
public abstract class TestContainerBase
{
    private PostgreSqlContainer container = null!;
    protected TimesheetDbContext Db = null!;

    [SetUp]
    public async Task SetUp()
    {
        container = new PostgreSqlBuilder("postgres:17").Build();
        await container.StartAsync();

        var options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseNpgsql(container.GetConnectionString())
            .Options;
        Db = new(options);
        await Db.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await Db.DisposeAsync();
        await container.DisposeAsync();
    }
}

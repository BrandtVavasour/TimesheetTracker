using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Regression guard for the Blazor Server DbContext concurrency crash. A single
/// long-lived shared <see cref="TimesheetDbContext"/> threw
/// "A second operation was started on this context instance" whenever two things
/// (e.g. a page and its layout) queried the data layer at the same time on one
/// circuit. <see cref="TimesheetData"/> now pulls a fresh context per call from
/// <see cref="IDbContextFactory{TContext}"/>, so overlapping operations through a
/// single service instance must complete without throwing.
/// </summary>
[TestFixture]
public class TimesheetDataConcurrencyTests
{
    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        // Same wiring as production (ServiceCollectionExtensions): factory + a
        // scoped context resolved from it.
        services.AddDbContextFactory<TimesheetDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<TimesheetDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<TimesheetDbContext>>().CreateDbContext());
        services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        services.AddSingleton<IClock>(new StubClock(SeedData.Today));
        services.AddScoped<ICurrentUser>(_ => new StubCurrentUser(SeedData.DemoUserId));
        services.AddScoped<ITimesheetData, TimesheetData>();
        return services.BuildServiceProvider();
    }

    [Test]
    public async Task ConcurrentOperations_OnOneServiceInstance_DoNotThrow()
    {
        await using var provider = BuildProvider("concurrency-" + Guid.NewGuid());
        await using (var seed = provider.CreateAsyncScope())
            await SeedData.SeedAsync(seed.ServiceProvider.GetRequiredService<TimesheetDbContext>());

        var data = provider.GetRequiredService<ITimesheetData>();
        var jobId = (await data.ActiveJobsAsync())[0].Id;
        var from = SeedData.Today.AddDays(-7);
        var to = SeedData.Today.AddDays(7);

        // Fan a burst of overlapping reads through the single service. With a
        // shared context the EF concurrency detector trips; with the per-call
        // factory each operation gets its own context and they all succeed.
        var ops = new List<Task>();
        for (var i = 0; i < 50; i++)
        {
            ops.Add(data.CurrentUserAsync());
            ops.Add(data.ActiveJobsAsync());
            ops.Add(data.AllJobsAsync());
            ops.Add(data.EntriesAsync(jobId, from, to));
        }

        var act = () => Task.WhenAll(ops);
        await act.Should().NotThrowAsync();
    }

    private sealed class StubCurrentUser(Guid id) : ICurrentUser
    {
        public Task<Guid?> GetIdAsync() => Task.FromResult<Guid?>(id);
    }

    private sealed class StubClock(DateOnly today) : IClock
    {
        public DateOnly Today => today;
    }
}

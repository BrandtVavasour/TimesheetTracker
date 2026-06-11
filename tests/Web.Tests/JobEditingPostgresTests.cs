using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

/// <summary>
/// Drives the real <see cref="TimesheetData"/> service through the Jobs screen
/// flows exactly as Jobs.razor performs them: AddJob → JobAsync (detached draft)
/// → add custom fields / project codes with page-generated Guids → SaveJobAsync.
/// Regression guard for the production DbUpdateConcurrencyException ("expected
/// to affect 1 row(s), but actually affected 0"): children created with pre-set
/// ids and discovered via navigation were tracked as Modified instead of Added.
/// Tests isolate via distinct users (the per-user query filters scope everything).
/// </summary>
public abstract class JobEditingFlowTests
{
    /// <summary>A fresh user plus the real data service, wired as the app wires it.</summary>
    protected abstract Task<TimesheetData> NewUserDataAsync();

    [Test]
    public async Task NewJob_RenameAndSave_Persists()
    {
        var data = await NewUserDataAsync();
        var job = await data.AddJobAsync();

        var draft = (await data.JobAsync(job.Id))!;
        draft.Name = "Acme Corp";

        await data.SaveJobAsync(draft);

        (await data.JobAsync(job.Id))!.Name.Should().Be("Acme Corp");
    }

    [Test]
    public async Task NewJob_AddCustomFieldAndSave_PersistsField()
    {
        var data = await NewUserDataAsync();
        var job = await data.AddJobAsync();

        var draft = (await data.JobAsync(job.Id))!;
        // Exactly what Jobs.razor AddField does: page-generated Id, JobId left unset.
        draft.CustomFields.Add(new JobCustomField
        {
            Id = Guid.NewGuid(), Name = "Employee #", Value = "40192",
            ShowOnTimesheet = true, DisplayOrder = 0,
        });

        await data.SaveJobAsync(draft);

        var saved = (await data.JobAsync(job.Id))!;
        saved.CustomFields.Should().ContainSingle(f => f.Name == "Employee #" && f.Value == "40192");
    }

    [Test]
    public async Task NewJob_AddProjectCodeAndSave_PersistsCode()
    {
        var data = await NewUserDataAsync();
        var job = await data.AddJobAsync();

        var draft = (await data.JobAsync(job.Id))!;
        // Exactly what Jobs.razor AddCode does.
        draft.ProjectCodes.Add(new ProjectCode
        {
            Id = Guid.NewGuid(), Code = "PC-100", Description = "Main project",
            IsActive = true, DisplayOrder = 0,
        });

        await data.SaveJobAsync(draft);

        var saved = (await data.JobAsync(job.Id))!;
        saved.ProjectCodes.Should().ContainSingle(c => c.Code == "PC-100");
    }

    [Test]
    public async Task SecondSave_EditFieldAndAddAnother_Persists()
    {
        var data = await NewUserDataAsync();
        var job = await data.AddJobAsync();

        var draft = (await data.JobAsync(job.Id))!;
        draft.CustomFields.Add(new JobCustomField
        {
            Id = Guid.NewGuid(), Name = "Employee #", Value = "1", DisplayOrder = 0,
        });
        await data.SaveJobAsync(draft);

        // The page keeps the same draft after Save; user edits and saves again.
        draft.CustomFields.First().Value = "2";
        draft.CustomFields.Add(new JobCustomField
        {
            Id = Guid.NewGuid(), Name = "Cost centre", Value = "CC-9", DisplayOrder = 1,
        });
        await data.SaveJobAsync(draft);

        var saved = (await data.JobAsync(job.Id))!;
        saved.CustomFields.Should().HaveCount(2);
        saved.CustomFields.Single(f => f.Name == "Employee #").Value.Should().Be("2");
    }

    [Test]
    public async Task Save_RemovingField_DeletesIt()
    {
        var data = await NewUserDataAsync();
        var job = await data.AddJobAsync();

        var draft = (await data.JobAsync(job.Id))!;
        draft.CustomFields.Add(new JobCustomField
        {
            Id = Guid.NewGuid(), Name = "Temp", Value = "x", DisplayOrder = 0,
        });
        await data.SaveJobAsync(draft);

        var draft2 = (await data.JobAsync(job.Id))!;
        draft2.CustomFields.Clear();
        await data.SaveJobAsync(draft2);

        (await data.JobAsync(job.Id))!.CustomFields.Should().BeEmpty();
    }

    protected static async Task<TimesheetData> NewUserDataAsync(DbContextOptions<TimesheetDbContext> options)
    {
        var userId = Guid.NewGuid();
        await using (var db = new TimesheetDbContext(options))
        {
            db.Users.Add(new AppUser
            {
                Id = userId,
                UserName = $"{userId:N}@example.com",
                Email = $"{userId:N}@example.com",
                DisplayName = "Test User",
                DefaultState = AustralianState.VIC,
            });
            await db.SaveChangesAsync();
        }
        return new TimesheetData(new OptionsDbFactory(options), new StubCurrentUser(userId), new StubClock(new(2026, 6, 11)));
    }

    private sealed class OptionsDbFactory(DbContextOptions<TimesheetDbContext> options) : IDbContextFactory<TimesheetDbContext>
    {
        public TimesheetDbContext CreateDbContext() => new(options);
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

/// <summary>Fast, CI-runnable mirror of the flows on the InMemory provider.</summary>
[TestFixture]
public class JobEditingInMemoryTests : JobEditingFlowTests
{
    private DbContextOptions<TimesheetDbContext> _options = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp() =>
        _options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseInMemoryDatabase("job-editing-" + Guid.NewGuid())
            .Options;

    protected override Task<TimesheetData> NewUserDataAsync() => NewUserDataAsync(_options);
}

/// <summary>The authoritative reproduction on real Postgres (matches production).</summary>
[TestFixture]
[Explicit("Requires Docker")]
[Category("Docker")]
public class JobEditingPostgresTests : JobEditingFlowTests
{
    private PostgreSqlContainer _container = null!;
    private DbContextOptions<TimesheetDbContext> _options = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _container = new PostgreSqlBuilder("postgres:17").Build();
        await _container.StartAsync();
        _options = new DbContextOptionsBuilder<TimesheetDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        await using var db = new TimesheetDbContext(_options);
        await db.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown() => await _container.DisposeAsync();

    protected override Task<TimesheetData> NewUserDataAsync() => NewUserDataAsync(_options);
}

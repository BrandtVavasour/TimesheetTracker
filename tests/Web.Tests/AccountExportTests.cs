using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.DataModel.Enums;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

[TestFixture]
public class AccountExportTests
{
    private DbContextOptions<TimesheetDbContext> options = null!;

    [SetUp]
    public void SetUp() => options = new DbContextOptionsBuilder<TimesheetDbContext>()
        .UseInMemoryDatabase("exp-" + Guid.NewGuid()).Options;

    private TimesheetData DataFor(Guid id) => new(new Factory(options), new Stub(id), new Clock());

    private AccountExport ExportFor(Guid id) =>
        new(DataFor(id), new ExportService(new TimeCalculationService(), new HolidayService()), new TimeCalculationService());

    [Test]
    public async Task BuildZip_ContainsAccountJson_PlusXlsxAndJsonPerJob_WithEntryData()
    {
        var id = Guid.NewGuid();
        await using (var db = new TimesheetDbContext(options))
        {
            db.Users.Add(new()
                { Id = id, UserName = "u@example.com", Email = "u@example.com", DisplayName = "Test User", DefaultState = AustralianState.NSW });
            await db.SaveChangesAsync();
        }
        var data = DataFor(id);
        var job = await data.AddJobAsync();
        var clone = await data.JobAsync(job.Id);
        clone!.Name = "Acme Corp";
        clone.ProjectCodes.Add(new() { Id = Guid.NewGuid(), Code = "PRJ-1234", IsActive = true });
        await data.SaveJobAsync(clone);
        var code = (await data.JobAsync(job.Id))!.ProjectCodes.First();
        await data.SaveEntryAsync(job.Id, new()
        {
            Id = Guid.NewGuid(), WorkDate = new(2026, 6, 10), StartTime = new(9, 0), EndTime = new(17, 0),
            BreakMinutes = 30, Notes = "Sprint planning", ProjectCodeId = code.Id,
        });

        var bytes = await ExportFor(id).BuildZipAsync();

        // ZIP magic.
        bytes[0].Should().Be((byte)'P');
        bytes[1].Should().Be((byte)'K');

        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName).ToList();

        names.Should().Contain("account.json");
        names.Should().Contain(n => n.StartsWith("jobs/") && n.EndsWith(".xlsx"));
        var jobJsonName = names.Single(n => n.StartsWith("jobs/") && n.EndsWith(".json"));
        jobJsonName.Should().Contain("Acme-Corp");

        // The job JSON carries the structured entry data.
        var jobJson = await ReadEntry(zip, jobJsonName);
        jobJson.Should().Contain("Acme Corp");
        jobJson.Should().Contain("Sprint planning");
        jobJson.Should().Contain("PRJ-1234");

        // account.json carries the profile.
        var account = await ReadEntry(zip, "account.json");
        account.Should().Contain("Test User");
        account.Should().Contain("u@example.com");
    }

    private static async Task<string> ReadEntry(ZipArchive zip, string name)
    {
        await using var s = zip.GetEntry(name)!.Open();
        using var r = new StreamReader(s, Encoding.UTF8);
        return await r.ReadToEndAsync();
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

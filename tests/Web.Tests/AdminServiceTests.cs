using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TimesheetTracker.DataModel;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

[TestFixture]
public class AdminServiceTests
{
    private static AdminService NewService(IdentityTestHost host) =>
        new(host.Users, host.Roles,
            host.Provider.GetRequiredService<IOptions<IdentityOptions>>(),
            host.Provider.GetRequiredService<IDbContextFactory<TimesheetDbContext>>());

    [Test]
    public async Task GetUsers_ListsEveryone_WithAdminFlagSetForGrantedUsers()
    {
        using var host = new IdentityTestHost();
        await host.CreateUserAsync("admin@example.com");
        await host.CreateUserAsync("normal@example.com");
        await AdminBootstrap.EnsureAdminsAsync(host.Provider, "admin@example.com", NullLogger.Instance);

        var users = await NewService(host).GetUsersAsync();

        users.Should().HaveCount(2);
        users.Single(u => u.Email == "admin@example.com").IsAdmin.Should().BeTrue();
        users.Single(u => u.Email == "normal@example.com").IsAdmin.Should().BeFalse();
    }

    [Test]
    public async Task GetUsers_ExposesLockoutStatusAndReason()
    {
        using var host = new IdentityTestHost();
        await host.CreateUserAsync("locked@example.com", lockedOut: true, failedCount: 5);
        await host.CreateUserAsync("ok@example.com");

        var users = await NewService(host).GetUsersAsync();

        var locked = users.Single(u => u.Email == "locked@example.com");
        locked.IsLockedOut.Should().BeTrue();
        locked.LockoutEnd.Should().NotBeNull();
        locked.AccessFailedCount.Should().Be(5);
        locked.MaxFailedAccessAttempts.Should().Be(5);

        users.Single(u => u.Email == "ok@example.com").IsLockedOut.Should().BeFalse();
    }

    [Test]
    public async Task GetUsers_ReportsHasPassword()
    {
        using var host = new IdentityTestHost();
        await host.CreateUserAsync("haspw@example.com", password: "Secret123!");
        await host.CreateUserAsync("googleonly@example.com"); // created without a password

        var users = await NewService(host).GetUsersAsync();

        users.Single(u => u.Email == "haspw@example.com").HasPassword.Should().BeTrue();
        users.Single(u => u.Email == "googleonly@example.com").HasPassword.Should().BeFalse();
    }

    [Test]
    public async Task GetUsers_WhenAdminRoleNeverCreated_DoesNotThrow_AndNobodyIsAdmin()
    {
        using var host = new IdentityTestHost();
        await host.CreateUserAsync("a@example.com");

        var users = await NewService(host).GetUsersAsync();

        users.Should().OnlyContain(u => u.IsAdmin == false);
    }

    [Test]
    public async Task Unlock_ClearsLockoutEndAndFailedCount()
    {
        using var host = new IdentityTestHost();
        var user = await host.CreateUserAsync("locked@example.com", lockedOut: true, failedCount: 5);
        var service = NewService(host);

        var ok = await service.UnlockAsync(user.Id);

        ok.Should().BeTrue();
        var after = (await service.GetUsersAsync()).Single(u => u.Id == user.Id);
        after.IsLockedOut.Should().BeFalse();
        after.AccessFailedCount.Should().Be(0);
        after.LockoutEnd.Should().BeNull();
    }

    [Test]
    public async Task Unlock_UnknownUser_ReturnsFalse()
    {
        using var host = new IdentityTestHost();

        (await NewService(host).UnlockAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task GetUsers_CountsJobsAndEntriesPerUser()
    {
        using var host = new IdentityTestHost();
        var a = await host.CreateUserAsync("a@example.com");
        var b = await host.CreateUserAsync("b@example.com");
        await host.SeedJobsAndEntriesAsync(a.Id, jobs: 2, entriesPerJob: 3); // 2 jobs, 6 entries
        await host.SeedJobsAndEntriesAsync(b.Id, jobs: 1, entriesPerJob: 0); // 1 job, 0 entries

        var users = await NewService(host).GetUsersAsync();

        var ua = users.Single(u => u.Email == "a@example.com");
        ua.JobCount.Should().Be(2);
        ua.EntryCount.Should().Be(6);

        var ub = users.Single(u => u.Email == "b@example.com");
        ub.JobCount.Should().Be(1);
        ub.EntryCount.Should().Be(0);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using TimesheetTracker.Web.Services;

namespace Web.Tests;

[TestFixture]
public class AdminBootstrapTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task NoEmail_BreaksOut_NoRoleNoAssignment(string? envValue)
    {
        using var host = new IdentityTestHost();
        var user = await host.CreateUserAsync("a@example.com");

        await AdminBootstrap.EnsureAdminsAsync(host.Provider, envValue, NullLogger.Instance);

        (await host.Roles.RoleExistsAsync(AdminBootstrap.AdminRole)).Should().BeFalse(
            "no admin email means we must break out before touching roles");
        (await host.Users.IsInRoleAsync(user, AdminBootstrap.AdminRole)).Should().BeFalse();
    }

    [Test]
    public async Task MatchingUser_GetsAdminRole_AndRoleIsCreated()
    {
        using var host = new IdentityTestHost();
        var user = await host.CreateUserAsync("owner@example.com");

        await AdminBootstrap.EnsureAdminsAsync(host.Provider, "owner@example.com", NullLogger.Instance);

        (await host.Roles.RoleExistsAsync(AdminBootstrap.AdminRole)).Should().BeTrue();
        (await host.Users.IsInRoleAsync(user, AdminBootstrap.AdminRole)).Should().BeTrue();
    }

    [Test]
    public async Task EmailMatch_IsCaseInsensitive()
    {
        using var host = new IdentityTestHost();
        var user = await host.CreateUserAsync("Owner@Example.com");

        await AdminBootstrap.EnsureAdminsAsync(host.Provider, "owner@example.COM", NullLogger.Instance);

        (await host.Users.IsInRoleAsync(user, AdminBootstrap.AdminRole)).Should().BeTrue();
    }

    [Test]
    public async Task NonMatchingEmail_NotAssigned_ButRoleStillEnsured()
    {
        using var host = new IdentityTestHost();
        var other = await host.CreateUserAsync("someone@example.com");

        // Admin email has no matching user yet — nothing to assign, but the role
        // is provisioned so a later startup (after they register) can assign it.
        await AdminBootstrap.EnsureAdminsAsync(host.Provider, "notyet@example.com", NullLogger.Instance);

        (await host.Roles.RoleExistsAsync(AdminBootstrap.AdminRole)).Should().BeTrue();
        (await host.Users.IsInRoleAsync(other, AdminBootstrap.AdminRole)).Should().BeFalse();
    }

    [Test]
    public async Task Idempotent_SecondRun_StaysSingleAdmin()
    {
        using var host = new IdentityTestHost();
        var user = await host.CreateUserAsync("admin@example.com");

        await AdminBootstrap.EnsureAdminsAsync(host.Provider, "admin@example.com", NullLogger.Instance);
        await AdminBootstrap.EnsureAdminsAsync(host.Provider, "admin@example.com", NullLogger.Instance);

        (await host.Users.IsInRoleAsync(user, AdminBootstrap.AdminRole)).Should().BeTrue();
        (await host.Users.GetRolesAsync(user)).Count(r => r == AdminBootstrap.AdminRole).Should().Be(1);
    }

    [Test]
    public async Task MultipleEmails_CommaOrSemicolonSeparated_AllAssigned()
    {
        using var host = new IdentityTestHost();
        var a = await host.CreateUserAsync("a@example.com");
        var b = await host.CreateUserAsync("b@example.com");
        var c = await host.CreateUserAsync("c@example.com");

        await AdminBootstrap.EnsureAdminsAsync(host.Provider, " a@example.com; b@example.com ,c@example.com ", NullLogger.Instance);

        (await host.Users.IsInRoleAsync(a, AdminBootstrap.AdminRole)).Should().BeTrue();
        (await host.Users.IsInRoleAsync(b, AdminBootstrap.AdminRole)).Should().BeTrue();
        (await host.Users.IsInRoleAsync(c, AdminBootstrap.AdminRole)).Should().BeTrue();
    }
}

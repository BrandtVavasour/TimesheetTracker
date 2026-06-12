using TimesheetTracker.Web.Services;

namespace Web.Tests;

[TestFixture]
public class AccountGraceTests
{
    private static readonly DateTimeOffset Created = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Period = AccountGrace.Period;

    [Test]
    public void VerifiedAccount_IsNeverBlocked_EvenLongAfterSignup() =>
        AccountGrace.IsBlocked(emailConfirmed: true, Created, Created.AddYears(1)).Should().BeFalse();

    [Test]
    public void Unverified_WithinWindow_IsNotBlocked() =>
        AccountGrace.IsBlocked(false, Created, Created + Period - TimeSpan.FromHours(1)).Should().BeFalse();

    [Test]
    public void Unverified_AtOrPastWindow_IsBlocked()
    {
        AccountGrace.IsBlocked(false, Created, Created + Period).Should().BeTrue();
        AccountGrace.IsBlocked(false, Created, Created + Period + TimeSpan.FromSeconds(1)).Should().BeTrue();
    }

    [Test]
    public void DaysLeft_CountsDownAndFloorsAtZero()
    {
        AccountGrace.DaysLeft(Created, Created).Should().Be((int)Math.Ceiling(Period.TotalDays));
        AccountGrace.DaysLeft(Created, Created + Period - TimeSpan.FromHours(1)).Should().Be(1);
        AccountGrace.DaysLeft(Created, Created + Period).Should().Be(0);
        AccountGrace.DaysLeft(Created, Created + Period + TimeSpan.FromDays(30)).Should().Be(0);
    }

    [Test]
    public void Expiry_IsOnePeriodAfterCreation() =>
        AccountGrace.Expiry(Created).Should().Be(Created + Period);
}

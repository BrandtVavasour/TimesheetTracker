using TimesheetTracker.Web.Services;

namespace Web.Tests;

[TestFixture]
public class AccountGraceTests
{
    private static readonly DateTimeOffset Created = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void VerifiedAccount_IsNeverBlocked_EvenLongAfterSignup()
    {
        AccountGrace.IsBlocked(emailConfirmed: true, Created, Created.AddYears(1)).Should().BeFalse();
    }

    [Test]
    public void Unverified_WithinWindow_IsNotBlocked()
    {
        AccountGrace.IsBlocked(false, Created, Created.AddDays(6)).Should().BeFalse();
    }

    [Test]
    public void Unverified_AtOrPastWindow_IsBlocked()
    {
        AccountGrace.IsBlocked(false, Created, Created.AddDays(7)).Should().BeTrue();
        AccountGrace.IsBlocked(false, Created, Created.AddDays(7).AddSeconds(1)).Should().BeTrue();
    }

    [Test]
    public void DaysLeft_CountsDownAndFloorsAtZero()
    {
        AccountGrace.DaysLeft(Created, Created).Should().Be(7);
        AccountGrace.DaysLeft(Created, Created.AddDays(6.1)).Should().Be(1);
        AccountGrace.DaysLeft(Created, Created.AddDays(7)).Should().Be(0);
        AccountGrace.DaysLeft(Created, Created.AddDays(30)).Should().Be(0);
    }

    [Test]
    public void Expiry_IsSevenDaysAfterCreation()
    {
        AccountGrace.Expiry(Created).Should().Be(Created.AddDays(7));
    }
}

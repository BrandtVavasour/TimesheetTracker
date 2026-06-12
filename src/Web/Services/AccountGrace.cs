namespace TimesheetTracker.Web.Services;

/// <summary>
/// Email-verification grace policy: a new local account may use the app for
/// <see cref="Period"/> after sign-up, after which access is blocked until the
/// email is verified. Google accounts are created already-verified, so they are
/// never affected. Pure functions — all "now"/created-at values are passed in.
/// </summary>
public static class AccountGrace
{
    /// <summary>How long an unverified account may use the app before being blocked.</summary>
    public static readonly TimeSpan Period = TimeSpan.FromDays(1);

    /// <summary>The moment an unverified account's access is cut off.</summary>
    public static DateTimeOffset Expiry(DateTimeOffset createdAt) => createdAt + Period;

    /// <summary>True once an unverified account has run past its grace window.
    /// A verified account is never blocked.</summary>
    public static bool IsBlocked(bool emailConfirmed, DateTimeOffset createdAt, DateTimeOffset now) =>
        !emailConfirmed && now >= createdAt + Period;

    /// <summary>Whole days left in the grace window (never negative), rounded up,
    /// for the reminder banner. Returns 0 once the window has closed.</summary>
    public static int DaysLeft(DateTimeOffset createdAt, DateTimeOffset now)
    {
        var remaining = createdAt + Period - now;
        return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalDays);
    }
}

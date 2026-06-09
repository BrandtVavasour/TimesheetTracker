using TimesheetTracker.DataModel;

namespace TimesheetTracker.Web.Services;

/// <summary>The signed-in user's id. Implementations resolve it from the auth context.</summary>
public interface ICurrentUser
{
    Guid Id { get; }
}

/// <summary>
/// Development stand-in: the seeded demo user. Replaced by an Identity-backed
/// implementation once the sign-in flow is wired.
/// </summary>
public sealed class DemoCurrentUser : ICurrentUser
{
    public Guid Id => SeedData.DemoUserId;
}

namespace TimesheetTracker.Web.Services;

/// <summary>The current date, for "today" highlighting and default week/month.</summary>
public interface IClock
{
    DateOnly Today { get; }
}

/// <summary>Real clock in Australian Eastern time (the app's wall-clock convention).</summary>
public sealed class SystemClock : IClock
{
    private static readonly TimeZoneInfo Tz = ResolveTimeZone();

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz));

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "Australia/Sydney", "AUS Eastern Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }
}

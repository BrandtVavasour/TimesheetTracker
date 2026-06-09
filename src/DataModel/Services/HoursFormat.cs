namespace TimesheetTracker.DataModel.Services;

/// <summary>
/// Display formatting for aggregated minute totals (days, weeks, periods).
/// Per-entry values come from <see cref="ITimeCalculationService"/>; this formats
/// summed minutes the same way for totals.
/// </summary>
public static class HoursFormat
{
    /// <summary>Decimal hours as a fixed-precision string, e.g. 450 min, 2dp => "7.50".</summary>
    public static string Decimal(int minutes, int places) =>
        (minutes / 60m).ToString("F" + Math.Clamp(places, 0, 6));

    /// <summary>Hours and minutes, e.g. 450 => "7:30".</summary>
    public static string Hmm(int minutes)
    {
        var negative = minutes < 0;
        minutes = Math.Abs(minutes);
        return $"{(negative ? "-" : "")}{minutes / 60}:{minutes % 60:D2}";
    }
}

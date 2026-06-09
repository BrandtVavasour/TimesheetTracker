namespace TimesheetTracker.DataModel.Services;

public class TimeCalculationService : ITimeCalculationService
{
    public int DurationMinutes(TimeEntry entry)
    {
        var start = entry.StartTime.ToTimeSpan();
        var end = entry.EndTime.ToTimeSpan();
        if (entry.EndsNextDay)
        {
            end += TimeSpan.FromDays(1);
        }

        var worked = (int)(end - start).TotalMinutes - entry.BreakMinutes;
        return worked > 0 ? worked : 0;
    }

    public decimal DecimalHours(TimeEntry entry, int decimalPlaces)
    {
        var hours = DurationMinutes(entry) / 60m;
        return Math.Round(hours, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public string HoursMinutes(TimeEntry entry)
    {
        var minutes = DurationMinutes(entry);
        return $"{minutes / 60}:{minutes % 60:D2}";
    }
}

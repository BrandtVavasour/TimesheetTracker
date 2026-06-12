using System.Collections.Concurrent;
using AustralianHolidays;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

/// <summary>
/// Public-holiday lookups, memoised per (date, state). The service is scoped
/// (per circuit / request), and holiday rules for a given date+state are stable,
/// so caching removes the repeated computation the weekly/calendar grids and the
/// export would otherwise do on every render / per cell.
/// </summary>
public class HolidayService : IHolidayService
{
    private readonly ConcurrentDictionary<(DateOnly Date, AustralianState State), (bool IsHoliday, string? Name)> _cache = new();

    public bool IsPublicHoliday(DateOnly date, AustralianState state, out string? name)
    {
        var result = _cache.GetOrAdd((date, state), key =>
        {
            var isHoliday = Holidays.IsHoliday(key.Date, key.State.ToHolidayState(), out var holidayName);
            return (isHoliday, holidayName);
        });
        name = result.Name;
        return result.IsHoliday;
    }
}

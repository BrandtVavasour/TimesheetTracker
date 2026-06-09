using AustralianHolidays;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

public class HolidayService : IHolidayService
{
    public bool IsPublicHoliday(DateOnly date, AustralianState state, out string? name) =>
        Holidays.IsHoliday(date, state.ToHolidayState(), out name);
}

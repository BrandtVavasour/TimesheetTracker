using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

public interface IHolidayService
{
    bool IsPublicHoliday(DateOnly date, AustralianState state, out string? name);
}

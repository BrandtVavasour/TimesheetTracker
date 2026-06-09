namespace TimesheetTracker.DataModel.Services;

public interface ITimeCalculationService
{
    int DurationMinutes(TimeEntry entry);
    decimal DecimalHours(TimeEntry entry, int decimalPlaces);
    string HoursMinutes(TimeEntry entry);
}

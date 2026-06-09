using AustralianHolidays;
using TimesheetTracker.DataModel.Enums;

namespace TimesheetTracker.DataModel.Services;

internal static class StateMappingExtensions
{
    public static State ToHolidayState(this AustralianState state) => state switch
    {
        AustralianState.NSW => State.NSW,
        AustralianState.VIC => State.VIC,
        AustralianState.QLD => State.QLD,
        AustralianState.SA => State.SA,
        AustralianState.WA => State.WA,
        AustralianState.TAS => State.TAS,
        AustralianState.NT => State.NT,
        AustralianState.ACT => State.ACT,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}

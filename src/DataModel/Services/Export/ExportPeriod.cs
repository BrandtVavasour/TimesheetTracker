namespace TimesheetTracker.DataModel.Services.Export;

public enum ExportScope { Week, Month, FinancialYear }

public static class ExportPeriod
{
    /// <summary>Returns the inclusive [start, end] date range for the scope containing <paramref name="anchor"/>.</summary>
    public static (DateOnly Start, DateOnly End) Range(ExportScope scope, DateOnly anchor) => scope switch
    {
        ExportScope.Week => WeekRange(anchor),
        ExportScope.Month => (new(anchor.Year, anchor.Month, 1),
                              new(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month))),
        ExportScope.FinancialYear => FinancialYearRange(anchor),
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static (DateOnly, DateOnly) WeekRange(DateOnly anchor)
    {
        var delta = ((int)anchor.DayOfWeek + 6) % 7; // Monday = 0
        var monday = anchor.AddDays(-delta);
        return (monday, monday.AddDays(6));
    }

    private static (DateOnly, DateOnly) FinancialYearRange(DateOnly anchor)
    {
        var startYear = anchor.Month >= 7 ? anchor.Year : anchor.Year - 1;
        return (new(startYear, 7, 1), new(startYear + 1, 6, 30));
    }
}

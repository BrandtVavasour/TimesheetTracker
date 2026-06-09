namespace TimesheetTracker.Web.Services;

/// <summary>Week/financial-year display helpers (week starts Monday; FY = 1 Jul–30 Jun).</summary>
public static class CalendarFormat
{
    public static readonly string[] DowShort = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    public static readonly string[] DowFull = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];
    public static readonly string[] MonthShort = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>0 = Monday … 6 = Sunday.</summary>
    public static int DowIndex(DateOnly d) => ((int)d.DayOfWeek + 6) % 7;

    public static DateOnly StartOfWeek(DateOnly d) => d.AddDays(-DowIndex(d));

    public static IReadOnlyList<DateOnly> WeekDates(DateOnly monday) =>
        Enumerable.Range(0, 7).Select(monday.AddDays).ToList();

    public static string DayLong(DateOnly d) => $"{DowFull[DowIndex(d)]} {d.Day} {MonthShort[d.Month - 1]}";

    public static string Range(DateOnly a, DateOnly b)
    {
        if (a.Month == b.Month) return $"{a.Day}–{b.Day} {MonthShort[a.Month - 1]} {b.Year}";
        return $"{a.Day} {MonthShort[a.Month - 1]} – {b.Day} {MonthShort[b.Month - 1]} {b.Year}";
    }

    public static string FyLabel(DateOnly d)
    {
        var startYear = d.Month >= 7 ? d.Year : d.Year - 1;
        return $"FY{startYear % 100:D2}/{(startYear + 1) % 100:D2}";
    }
}

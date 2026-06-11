using Excelsior;

namespace TimesheetTracker.DataModel.Services.Export;

public class TimesheetRow
{
    [Column(Order = 1)]
    public string Date { get; set; } = null!;

    [Column(Order = 2)]
    public string Day { get; set; } = null!;

    [Column(Order = 3)]
    public string Start { get; set; } = null!;

    [Column(Order = 4)]
    public string End { get; set; } = null!;

    [Column(Heading = "Break (min)", Order = 5)]
    public int BreakMinutes { get; set; }

    [Column(Heading = "Hours (decimal)", Order = 6)]
    public decimal DecimalHours { get; set; }

    [Column(Heading = "Hours (h:mm)", Order = 7)]
    public string HoursMinutes { get; set; } = null!;

    [Column(Order = 8)]
    public string? ProjectCode { get; set; }

    [Column(Heading = "WFH", Order = 9)]
    public string? WorkFromHome { get; set; }

    [Column(Order = 10)]
    public string? PublicHoliday { get; set; }

    [Column(Order = 11)]
    public string? Notes { get; set; }
}

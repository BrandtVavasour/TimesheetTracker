using Microsoft.AspNetCore.Identity;
using TimesheetTracker.DataModel.Enums;

public class AppUser : IdentityUser<Guid>
{
    [MaxLength(200)]
    public string DisplayName { get; set; } = null!;

    public AustralianState DefaultState { get; set; } = AustralianState.NSW;

    /// <summary>When the account was created. Drives the email-verification grace
    /// window for unverified local accounts.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When true, exports and the export preview include every calendar
    /// day in the period (blank on days not worked), not just days with entries.</summary>
    public bool ExportIncludeAllDays { get; set; } = true;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}

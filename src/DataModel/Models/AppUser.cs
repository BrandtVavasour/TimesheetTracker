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

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}

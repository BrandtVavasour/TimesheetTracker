using Microsoft.AspNetCore.Identity;
using TimesheetTracker.DataModel.Enums;

public class AppUser : IdentityUser<Guid>
{
    [MaxLength(200)]
    public string DisplayName { get; set; } = null!;

    public AustralianState DefaultState { get; set; } = AustralianState.NSW;

    public ICollection<Job> Jobs { get; set; } = new List<Job>();
}

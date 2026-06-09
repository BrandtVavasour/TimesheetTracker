using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimesheetTracker.DataModel.Enums;

public class Job : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>Overrides the user's DefaultState for holiday calculation when set.</summary>
    public AustralianState? StateOverride { get; set; }

    /// <summary>Decimal places for the copyable decimal-hours output. Default 2.</summary>
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>Scheduled work days; drives grid emphasis only, never blocks entry.</summary>
    public DaysOfWeek WorkDays { get; set; } = DaysOfWeek.Weekdays;

    public bool IsArchived { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<JobCustomField> CustomFields { get; set; } = new List<JobCustomField>();
    public ICollection<ProjectCode> ProjectCodes { get; set; } = new List<ProjectCode>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();

    public class Configuration : IEntityTypeConfiguration<Job>
    {
        public void Configure(EntityTypeBuilder<Job> builder)
        {
            builder
                .HasOne(j => j.User)
                .WithMany(u => u.Jobs)
                .HasForeignKey(j => j.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(j => j.UserId);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TimeEntry : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    /// <summary>The day the entry is filed under. For a midnight-spanner, the day it starts.</summary>
    public DateOnly WorkDate { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>True when the shift ends after midnight on the following day.</summary>
    public bool EndsNextDay { get; set; }

    public int BreakMinutes { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid? ProjectCodeId { get; set; }
    public ProjectCode? ProjectCode { get; set; }

    public class Configuration : IEntityTypeConfiguration<TimeEntry>
    {
        public void Configure(EntityTypeBuilder<TimeEntry> builder)
        {
            builder
                .HasOne(e => e.Job)
                .WithMany(j => j.TimeEntries)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(e => e.ProjectCode)
                .WithMany(p => p.TimeEntries)
                .HasForeignKey(e => e.ProjectCodeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(e => new { e.JobId, e.WorkDate });
        }
    }
}

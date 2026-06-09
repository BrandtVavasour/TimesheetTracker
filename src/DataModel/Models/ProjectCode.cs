using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ProjectCode : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    [MaxLength(100)]
    public string Code { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();

    public class Configuration : IEntityTypeConfiguration<ProjectCode>
    {
        public void Configure(EntityTypeBuilder<ProjectCode> builder)
        {
            builder
                .HasOne(p => p.Job)
                .WithMany(j => j.ProjectCodes)
                .HasForeignKey(p => p.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => p.JobId);
        }
    }
}

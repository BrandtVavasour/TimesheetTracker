using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class JobCustomField : BaseEntity
{
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string Value { get; set; } = null!;

    /// <summary>When true, shown in the weekly grid and export header.</summary>
    public bool ShowOnTimesheet { get; set; }

    public int DisplayOrder { get; set; }

    public class Configuration : IEntityTypeConfiguration<JobCustomField>
    {
        public void Configure(EntityTypeBuilder<JobCustomField> builder)
        {
            builder
                .HasOne(f => f.Job)
                .WithMany(j => j.CustomFields)
                .HasForeignKey(f => f.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(f => f.JobId);
        }
    }
}

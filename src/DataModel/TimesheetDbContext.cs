using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TimesheetTracker.DataModel;

public class TimesheetDbContext(DbContextOptions<TimesheetDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Job> Jobs { get; set; }
    public DbSet<JobCustomField> JobCustomFields { get; set; }
    public DbSet<ProjectCode> ProjectCodes { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }

    /// <summary>
    /// The signed-in user's id. Set per request/circuit so global query filters scope
    /// every Job/TimeEntry/JobCustomField/ProjectCode read to the owner — defence in depth
    /// against cross-user (IDOR) access on top of explicit query scoping.
    /// </summary>
    public Guid CurrentUserId { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<AppUser>().ToTable("Users");
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("Roles");

        // Stamp account creation time at the database so existing rows and any
        // insert path get a value; registration also sets it explicitly.
        modelBuilder.Entity<AppUser>().Property(u => u.CreatedAt).HasDefaultValueSql("now()");

        // Per-user query filters (defence in depth).
        modelBuilder.Entity<Job>().HasQueryFilter(j => j.UserId == CurrentUserId);
        modelBuilder.Entity<TimeEntry>().HasQueryFilter(e => e.Job.UserId == CurrentUserId);
        modelBuilder.Entity<JobCustomField>().HasQueryFilter(f => f.Job.UserId == CurrentUserId);
        modelBuilder.Entity<ProjectCode>().HasQueryFilter(p => p.Job.UserId == CurrentUserId);
    }
}

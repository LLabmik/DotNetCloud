using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// EF Core configuration for WorkItemWatcher (join table).
/// Composite primary key on (WorkItemId, UserId).
/// </summary>
public sealed class WorkItemWatcherConfiguration : IEntityTypeConfiguration<WorkItemWatcher>
{
    public void Configure(EntityTypeBuilder<WorkItemWatcher> builder)
    {
        builder.HasKey(w => new { w.WorkItemId, w.UserId });

        builder.Property(w => w.SubscribedAt)
            .IsRequired();

        builder.HasOne(w => w.WorkItem)
            .WithMany(wi => wi.Watchers)
            .HasForeignKey(w => w.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("WorkItemWatchers", "tracks");
    }
}

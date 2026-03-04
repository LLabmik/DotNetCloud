using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="Announcement"/> entity.
/// </summary>
internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Title).IsRequired().HasMaxLength(500);
        builder.Property(a => a.Content).IsRequired();
        builder.Property(a => a.Priority).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => a.OrganizationId);
        builder.HasIndex(a => a.PublishedAt);
        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasMany(a => a.Acknowledgements)
            .WithOne(ack => ack.Announcement)
            .HasForeignKey(ack => ack.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

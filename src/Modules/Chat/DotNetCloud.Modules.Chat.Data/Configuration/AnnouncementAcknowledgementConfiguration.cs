using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="AnnouncementAcknowledgement"/> entity.
/// </summary>
internal sealed class AnnouncementAcknowledgementConfiguration : IEntityTypeConfiguration<AnnouncementAcknowledgement>
{
    public void Configure(EntityTypeBuilder<AnnouncementAcknowledgement> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.AnnouncementId, a.UserId }).IsUnique();
    }
}

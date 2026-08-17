using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserNotificationPreference"/> entity.
/// </summary>
public sealed class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.ToTable("UserNotificationPreferences");

        // One row per user.
        builder.HasKey(p => p.UserId);

        // The JSON column backs the domain model's IReadOnlySet<Guid> MutedChannelIds.
        builder.Property(p => p.MutedChannelIdsJson)
            .HasColumnName("MutedChannelIds");
    }
}

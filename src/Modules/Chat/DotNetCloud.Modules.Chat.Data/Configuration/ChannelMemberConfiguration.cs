using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ChannelMember"/> entity.
/// </summary>
public sealed class ChannelMemberConfiguration : IEntityTypeConfiguration<ChannelMember>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChannelMember> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(m => m.NotificationPref)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.JoinedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Composite unique index: one membership per user per channel
        builder.HasIndex(m => new { m.ChannelId, m.UserId })
            .IsUnique()
            .HasDatabaseName("ix_chat_channel_members_channel_user");

        // FK to Channel
        builder.HasOne(m => m.Channel)
            .WithMany(c => c.Members)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("ix_chat_channel_members_user_id");
    }
}

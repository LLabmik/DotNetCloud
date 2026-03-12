using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="ChannelInvite"/> entity.
/// </summary>
public sealed class ChannelInviteConfiguration : IEntityTypeConfiguration<ChannelInvite>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChannelInvite> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(i => i.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(i => i.Message)
            .HasMaxLength(500);

        // One pending invite per user per channel
        builder.HasIndex(i => new { i.ChannelId, i.InvitedUserId, i.Status })
            .HasDatabaseName("ix_chat_channel_invites_channel_user_status");

        // FK to Channel
        builder.HasOne(i => i.Channel)
            .WithMany(c => c.Invites)
            .HasForeignKey(i => i.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.InvitedUserId)
            .HasDatabaseName("ix_chat_channel_invites_invited_user");

        builder.HasIndex(i => i.InvitedByUserId)
            .HasDatabaseName("ix_chat_channel_invites_invited_by");
    }
}

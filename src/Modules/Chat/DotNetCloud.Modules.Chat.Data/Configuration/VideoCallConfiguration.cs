using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="VideoCall"/> entity.
/// </summary>
public sealed class VideoCallConfiguration : IEntityTypeConfiguration<VideoCall>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VideoCall> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(v => v.MediaType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(v => v.EndReason)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(v => v.LiveKitRoomId)
            .HasMaxLength(200);

        builder.Property(v => v.CreatedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft-delete query filter
        builder.HasQueryFilter(v => !v.IsDeleted);

        // FK to Channel
        builder.HasOne(v => v.Channel)
            .WithMany()
            .HasForeignKey(v => v.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(v => new { v.ChannelId, v.State })
            .HasDatabaseName("ix_chat_video_calls_channel_state");

        builder.HasIndex(v => v.InitiatorUserId)
            .HasDatabaseName("ix_chat_video_calls_initiator_user_id");

        builder.HasIndex(v => v.HostUserId)
            .HasDatabaseName("ix_chat_video_calls_host_user_id");

        builder.HasIndex(v => v.CreatedAtUtc)
            .HasDatabaseName("ix_chat_video_calls_created_at");

        builder.HasIndex(v => v.State)
            .HasDatabaseName("ix_chat_video_calls_state");

        builder.HasIndex(v => v.IsDeleted)
            .HasDatabaseName("ix_chat_video_calls_is_deleted");
    }
}

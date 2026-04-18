using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="CallParticipant"/> entity.
/// </summary>
public sealed class CallParticipantConfiguration : IEntityTypeConfiguration<CallParticipant>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CallParticipant> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.JoinedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Composite unique index: one participation per user per call
        builder.HasIndex(p => new { p.VideoCallId, p.UserId })
            .IsUnique()
            .HasDatabaseName("ix_chat_call_participants_call_user");

        // FK to VideoCall
        builder.HasOne(p => p.VideoCall)
            .WithMany(v => v.Participants)
            .HasForeignKey(p => p.VideoCallId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on UserId + JoinedAtUtc for call history queries
        builder.HasIndex(p => new { p.UserId, p.JoinedAtUtc })
            .HasDatabaseName("ix_chat_call_participants_user_joined");

        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("ix_chat_call_participants_user_id");
    }
}

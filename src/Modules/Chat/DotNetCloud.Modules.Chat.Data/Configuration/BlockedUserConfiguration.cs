using DotNetCloud.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Chat.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="BlockedUser"/> entity.
/// </summary>
public sealed class BlockedUserConfiguration : IEntityTypeConfiguration<BlockedUser>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BlockedUser> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BlockedAtUtc)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // One block record per (user, blocked-user) pair
        builder.HasIndex(b => new { b.UserId, b.BlockedUserId })
            .IsUnique()
            .HasDatabaseName("ix_chat_blocked_users_user_blocked");

        builder.HasIndex(b => b.BlockedUserId)
            .HasDatabaseName("ix_chat_blocked_users_blocked_user_id");
    }
}

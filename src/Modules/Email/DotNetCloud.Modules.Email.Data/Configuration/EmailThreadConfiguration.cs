using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailThread"/> entity.
/// </summary>
public sealed class EmailThreadConfiguration : IEntityTypeConfiguration<EmailThread>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailThread> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.ProviderThreadId).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Snippet).HasMaxLength(500);
        builder.Property(t => t.ParticipantsJson).HasColumnType("jsonb");
        builder.Property(t => t.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(t => t.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(t => t.AccountId).HasDatabaseName("ix_email_threads_account_id");
        builder.HasIndex(t => new { t.AccountId, t.ProviderThreadId }).IsUnique().HasDatabaseName("ix_email_threads_account_provider");
        builder.HasIndex(t => t.LastMessageAt).HasDatabaseName("ix_email_threads_last_message");
    }
}

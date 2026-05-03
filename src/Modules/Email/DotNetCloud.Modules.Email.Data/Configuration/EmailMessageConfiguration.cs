using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailMessage"/> entity.
/// </summary>
public sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ProviderMessageId).IsRequired().HasMaxLength(500);
        builder.Property(m => m.MessageIdHeader).HasMaxLength(500);
        builder.Property(m => m.InReplyTo).HasMaxLength(500);
        builder.Property(m => m.References).HasMaxLength(2000);
        builder.Property(m => m.FromJson).IsRequired().HasColumnType("jsonb");
        builder.Property(m => m.ToJson).HasColumnType("jsonb");
        builder.Property(m => m.CcJson).HasColumnType("jsonb");
        builder.Property(m => m.BccJson).HasColumnType("jsonb");
        builder.Property(m => m.Subject).IsRequired().HasMaxLength(500);
        builder.Property(m => m.BodyPreview).HasColumnType("text");
        builder.Property(m => m.BodyHtml).HasColumnType("text");
        builder.Property(m => m.FlagsJson).HasColumnType("jsonb");
        builder.Property(m => m.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(m => m.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasQueryFilter(m => !m.IsDeleted);

        builder.HasOne(m => m.Thread)
            .WithMany(t => t.Messages)
            .HasForeignKey(m => m.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.ThreadId).HasDatabaseName("ix_email_messages_thread_id");
        builder.HasIndex(m => m.AccountId).HasDatabaseName("ix_email_messages_account_id");
        builder.HasIndex(m => new { m.AccountId, m.ProviderMessageId }).IsUnique().HasDatabaseName("ix_email_messages_account_provider");
        builder.HasIndex(m => m.DateReceived).HasDatabaseName("ix_email_messages_date_received");
    }
}

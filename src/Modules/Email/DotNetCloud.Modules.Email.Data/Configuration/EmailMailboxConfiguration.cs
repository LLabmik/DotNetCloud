using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailMailbox"/> entity.
/// </summary>
public sealed class EmailMailboxConfiguration : IEntityTypeConfiguration<EmailMailbox>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailMailbox> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ProviderId).IsRequired().HasMaxLength(500);
        builder.Property(m => m.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(m => m.Account)
            .WithMany(a => a.Mailboxes)
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.AccountId).HasDatabaseName("ix_email_mailboxes_account_id");
        builder.HasIndex(m => new { m.AccountId, m.ProviderId }).IsUnique().HasDatabaseName("ix_email_mailboxes_account_provider");
    }
}

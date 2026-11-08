using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Email.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="EmailAccount"/> entity.
/// </summary>
public sealed class EmailAccountConfiguration : IEntityTypeConfiguration<EmailAccount>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EmailAccount> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.EmailAddress).IsRequired().HasMaxLength(320);
        builder.Property(a => a.EncryptedCredentialBlob).HasColumnType("text");
        builder.Property(a => a.SyncStateJson).HasColumnType("jsonb");
        builder.Property(a => a.ProviderType).HasConversion<int>();
        builder.Property(a => a.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasQueryFilter(a => !a.IsDeleted);

        builder.HasIndex(a => a.OwnerId).HasDatabaseName("ix_email_accounts_owner_id");
        builder.HasIndex(a => a.EmailAddress).HasDatabaseName("ix_email_accounts_email");
    }
}

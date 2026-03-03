using DotNetCloud.Core.Data.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Auth;

/// <summary>
/// EF Core fluent API configuration for the <see cref="FidoCredential"/> entity.
/// </summary>
public class FidoCredentialConfiguration : IEntityTypeConfiguration<FidoCredential>
{
    /// <summary>
    /// Configures the <see cref="FidoCredential"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<FidoCredential> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.CredentialId)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(e => e.PublicKey)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(e => e.SignatureCounter)
            .IsRequired()
            .HasDefaultValue(0u);

        builder.Property(e => e.DeviceName)
            .HasMaxLength(200);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_FidoCredentials_UserId");

        builder.HasIndex(e => e.CredentialId)
            .IsUnique()
            .HasDatabaseName("IX_FidoCredentials_CredentialId");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("FK_FidoCredentials_AspNetUsers_UserId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

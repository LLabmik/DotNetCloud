using DotNetCloud.Core.Data.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Auth;

/// <summary>
/// EF Core fluent API configuration for the <see cref="UserBackupCode"/> entity.
/// </summary>
public class UserBackupCodeConfiguration : IEntityTypeConfiguration<UserBackupCode>
{
    /// <summary>
    /// Configures the <see cref="UserBackupCode"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<UserBackupCode> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.CodeHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UsedAt)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_UserBackupCodes_UserId");

        builder.HasIndex(e => new { e.UserId, e.IsUsed })
            .HasDatabaseName("IX_UserBackupCodes_UserId_IsUsed");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .HasConstraintName("FK_UserBackupCodes_AspNetUsers_UserId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

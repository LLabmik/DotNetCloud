using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Identity;

/// <summary>
/// Entity Framework configuration for the ApplicationUser entity.
/// Configures properties, indexes, and relationships.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // DisplayName is required with max length
        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        // AvatarUrl optional with max length
        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        // Locale required with max length
        builder.Property(u => u.Locale)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("en-US");

        // Timezone required with max length
        builder.Property(u => u.Timezone)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("UTC");

        // CreatedAt required with default value
        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // LastLoginAt optional
        builder.Property(u => u.LastLoginAt)
            .IsRequired(false);

        // IsActive required with default value
        builder.Property(u => u.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // Indexes for common queries
        builder.HasIndex(u => u.DisplayName)
            .HasDatabaseName("IX_ApplicationUsers_DisplayName");

        builder.HasIndex(u => u.Email)
            .HasDatabaseName("IX_ApplicationUsers_Email");

        builder.HasIndex(u => u.IsActive)
            .HasDatabaseName("IX_ApplicationUsers_IsActive");

        builder.HasIndex(u => u.LastLoginAt)
            .HasDatabaseName("IX_ApplicationUsers_LastLoginAt");
    }
}

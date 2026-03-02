using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Data.Entities.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Core.Data.Configuration.Settings;

/// <summary>
/// EF Core fluent API configuration for the <see cref="UserSetting"/> entity.
/// </summary>
/// <remarks>
/// Configures the User Settings table with:
/// - Primary key on Id
/// - Composite unique constraint (UserId, Module, Key)
/// - Foreign key to ApplicationUser with cascade delete
/// - Required fields and constraints
/// - Indexes for query optimization
/// - IsEncrypted flag for sensitive data handling
/// - Table naming strategy application
/// </remarks>
public class UserSettingConfiguration : IEntityTypeConfiguration<UserSetting>
{
    /// <summary>
    /// Configures the <see cref="UserSetting"/> entity.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    public void Configure(EntityTypeBuilder<UserSetting> builder)
    {
        // Primary key
        builder.HasKey(s => s.Id);

        // Properties configuration
        builder.Property(s => s.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("key");

        builder.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(10000)
            .HasColumnName("value");

        builder.Property(s => s.Module)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("module");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAddOrUpdate();

        builder.Property(s => s.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.Property(s => s.IsEncrypted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_encrypted");

        // Unique constraint: each user can have only one setting per (Module, Key)
        builder.HasIndex(s => new { s.UserId, s.Module, s.Key })
            .IsUnique()
            .HasDatabaseName("IX_user_settings_unique");

        // Indexes for query optimization
        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("IX_user_settings_user_id");

        builder.HasIndex(s => s.Module)
            .HasDatabaseName("IX_user_settings_module");

        builder.HasIndex(s => s.UpdatedAt)
            .HasDatabaseName("IX_user_settings_updated_at");

        builder.HasIndex(s => s.IsEncrypted)
            .HasDatabaseName("IX_user_settings_is_encrypted");

        // Foreign key relationship to ApplicationUser
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_user_settings_user_id");

        // Table naming will be applied by ITableNamingStrategy during context configuration
        builder.ToTable("UserSettings");
    }
}

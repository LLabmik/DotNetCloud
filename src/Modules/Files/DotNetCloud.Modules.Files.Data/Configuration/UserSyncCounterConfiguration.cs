using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Files.Data.Configuration;

/// <summary>
/// EF Core configuration for the <see cref="UserSyncCounter"/> entity.
/// </summary>
public sealed class UserSyncCounterConfiguration : IEntityTypeConfiguration<UserSyncCounter>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserSyncCounter> builder)
    {
        builder.HasKey(c => c.UserId);

        builder.Property(c => c.CurrentSequence)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}

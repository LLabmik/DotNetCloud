using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

/// <summary>
/// EF Core configuration for <see cref="CustomView"/>.
/// </summary>
internal sealed class CustomViewConfiguration : IEntityTypeConfiguration<CustomView>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<CustomView> builder)
    {
        builder.ToTable("CustomViews", "tracks");

        builder.HasKey(cv => cv.Id);

        builder.Property(cv => cv.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(cv => cv.FilterJson)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(cv => cv.SortJson)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(cv => cv.GroupBy)
            .HasMaxLength(100);

        builder.Property(cv => cv.Layout)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(cv => new { cv.ProductId, cv.UserId, cv.Name })
            .IsUnique();

        builder.HasIndex(cv => cv.ProductId);

        builder.HasOne(cv => cv.Product)
            .WithMany()
            .HasForeignKey(cv => cv.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class TeamRoleConfiguration : IEntityTypeConfiguration<TeamRole>
{
    public void Configure(EntityTypeBuilder<TeamRole> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.AssignedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(r => new { r.TeamId, r.UserId })
            .IsUnique()
            .HasDatabaseName("uq_team_roles_team_user");

        builder.HasIndex(r => r.TeamId)
            .HasDatabaseName("ix_team_roles_team_id");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_team_roles_user_id");
    }
}

using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class WorkItemFieldValueConfiguration : IEntityTypeConfiguration<WorkItemFieldValue>
{
    public void Configure(EntityTypeBuilder<WorkItemFieldValue> builder)
    {
        builder.HasKey(fv => fv.Id);

        builder.Property(fv => fv.Value)
            .HasMaxLength(4000);

        builder.Property(fv => fv.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(fv => fv.WorkItem)
            .WithMany(wi => wi.FieldValues)
            .HasForeignKey(fv => fv.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(fv => fv.CustomField)
            .WithMany(cf => cf.FieldValues)
            .HasForeignKey(fv => fv.CustomFieldId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(fv => new { fv.WorkItemId, fv.CustomFieldId })
            .IsUnique()
            .HasDatabaseName("uq_workitem_fieldvalue_item_field");
    }
}

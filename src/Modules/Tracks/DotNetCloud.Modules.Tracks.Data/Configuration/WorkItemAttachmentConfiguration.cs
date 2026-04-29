using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class WorkItemAttachmentConfiguration : IEntityTypeConfiguration<WorkItemAttachment>
{
    public void Configure(EntityTypeBuilder<WorkItemAttachment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Url)
            .HasMaxLength(2000);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.MimeType)
            .HasMaxLength(255);

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(a => a.WorkItem)
            .WithMany(wi => wi.Attachments)
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.WorkItemId)
            .HasDatabaseName("ix_work_item_attachments_work_item");

        builder.HasIndex(a => a.FileNodeId)
            .HasDatabaseName("ix_work_item_attachments_file_node")
            .HasFilter("\"FileNodeId\" IS NOT NULL");
    }
}

using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Tests.DTOs.Search;

/// <summary>
/// Tests for the <see cref="SearchDocument"/> record.
/// </summary>
[TestClass]
public class SearchDocumentTests
{
    [TestMethod]
    public void SearchDocument_CanBeCreated_WithRequiredProperties()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Act
        var doc = new SearchDocument
        {
            ModuleId = "notes",
            EntityId = Guid.NewGuid().ToString(),
            EntityType = "Note",
            Title = "Quarterly Report",
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        Assert.AreEqual("notes", doc.ModuleId);
        Assert.AreEqual("Note", doc.EntityType);
        Assert.AreEqual("Quarterly Report", doc.Title);
        Assert.AreEqual(string.Empty, doc.Content);
        Assert.IsNull(doc.Summary);
        Assert.AreEqual(ownerId, doc.OwnerId);
        Assert.IsNull(doc.OrganizationId);
        Assert.AreEqual(0, doc.Metadata.Count);
    }

    [TestMethod]
    public void SearchDocument_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var metadata = new Dictionary<string, string> { ["MimeType"] = "application/pdf", ["Path"] = "/docs" };

        // Act
        var doc = new SearchDocument
        {
            ModuleId = "files",
            EntityId = Guid.NewGuid().ToString(),
            EntityType = "FileNode",
            Title = "Report.pdf",
            Content = "Extracted PDF text content here",
            Summary = "A quarterly report...",
            OwnerId = ownerId,
            OrganizationId = orgId,
            CreatedAt = now,
            UpdatedAt = now,
            Metadata = metadata
        };

        // Assert
        Assert.AreEqual("files", doc.ModuleId);
        Assert.AreEqual("Extracted PDF text content here", doc.Content);
        Assert.AreEqual("A quarterly report...", doc.Summary);
        Assert.AreEqual(orgId, doc.OrganizationId);
        Assert.AreEqual(2, doc.Metadata.Count);
        Assert.AreEqual("application/pdf", doc.Metadata["MimeType"]);
    }

    [TestMethod]
    public void SearchDocument_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var entityId = Guid.NewGuid().ToString();
        var ownerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var sharedMetadata = (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

        var doc1 = new SearchDocument
        {
            ModuleId = "notes",
            EntityId = entityId,
            EntityType = "Note",
            Title = "Test",
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now,
            Metadata = sharedMetadata
        };

        var doc2 = new SearchDocument
        {
            ModuleId = "notes",
            EntityId = entityId,
            EntityType = "Note",
            Title = "Test",
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now,
            Metadata = sharedMetadata
        };

        // Assert
        Assert.AreEqual(doc1, doc2);
    }
}

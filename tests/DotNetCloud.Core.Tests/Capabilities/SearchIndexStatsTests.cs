using DotNetCloud.Core.Capabilities;

namespace DotNetCloud.Core.Tests.Capabilities;

/// <summary>
/// Tests for the <see cref="SearchIndexStats"/> record.
/// </summary>
[TestClass]
public class SearchIndexStatsTests
{
    [TestMethod]
    public void SearchIndexStats_CanBeCreated_WithRequiredProperties()
    {
        // Arrange
        var docsPerModule = new Dictionary<string, int>
        {
            ["files"] = 500,
            ["notes"] = 120,
            ["chat"] = 3400
        };

        // Act
        var stats = new SearchIndexStats
        {
            TotalDocuments = 4020,
            DocumentsPerModule = docsPerModule
        };

        // Assert
        Assert.AreEqual(4020, stats.TotalDocuments);
        Assert.AreEqual(3, stats.DocumentsPerModule.Count);
        Assert.AreEqual(500, stats.DocumentsPerModule["files"]);
        Assert.IsNull(stats.LastFullReindexAt);
        Assert.IsNull(stats.LastIncrementalIndexAt);
    }

    [TestMethod]
    public void SearchIndexStats_CanBeCreated_WithTimestamps()
    {
        // Arrange
        var lastReindex = DateTimeOffset.UtcNow.AddHours(-6);
        var lastIncremental = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Act
        var stats = new SearchIndexStats
        {
            TotalDocuments = 1000,
            DocumentsPerModule = new Dictionary<string, int> { ["notes"] = 1000 },
            LastFullReindexAt = lastReindex,
            LastIncrementalIndexAt = lastIncremental
        };

        // Assert
        Assert.AreEqual(lastReindex, stats.LastFullReindexAt);
        Assert.AreEqual(lastIncremental, stats.LastIncrementalIndexAt);
    }
}

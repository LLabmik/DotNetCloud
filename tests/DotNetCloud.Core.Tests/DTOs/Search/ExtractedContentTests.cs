using DotNetCloud.Core.DTOs.Search;

namespace DotNetCloud.Core.Tests.DTOs.Search;

/// <summary>
/// Tests for the <see cref="ExtractedContent"/> record.
/// </summary>
[TestClass]
public class ExtractedContentTests
{
    [TestMethod]
    public void ExtractedContent_CanBeCreated_WithTextOnly()
    {
        // Act
        var content = new ExtractedContent
        {
            Text = "This is extracted plain text from a PDF document."
        };

        // Assert
        Assert.AreEqual("This is extracted plain text from a PDF document.", content.Text);
        Assert.AreEqual(0, content.Metadata.Count);
    }

    [TestMethod]
    public void ExtractedContent_CanBeCreated_WithMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["Author"] = "Jane Doe",
            ["Title"] = "Quarterly Report Q1 2026",
            ["PageCount"] = "12"
        };

        // Act
        var content = new ExtractedContent
        {
            Text = "Full extracted text here...",
            Metadata = metadata
        };

        // Assert
        Assert.AreEqual("Full extracted text here...", content.Text);
        Assert.AreEqual(3, content.Metadata.Count);
        Assert.AreEqual("Jane Doe", content.Metadata["Author"]);
        Assert.AreEqual("12", content.Metadata["PageCount"]);
    }

    [TestMethod]
    public void ExtractedContent_RecordEquality_WorksCorrectly()
    {
        // Arrange — records with reference-type properties use reference equality for those members,
        // so share the same Metadata instance to verify value-type fields participate in equality.
        var sharedMetadata = (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();
        var content1 = new ExtractedContent { Text = "Hello world", Metadata = sharedMetadata };
        var content2 = new ExtractedContent { Text = "Hello world", Metadata = sharedMetadata };

        // Assert
        Assert.AreEqual(content1, content2);
    }
}

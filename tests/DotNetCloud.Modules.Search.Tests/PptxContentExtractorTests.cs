using DotNetCloud.Modules.Search.Extractors;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="PptxContentExtractor"/>.
/// </summary>
[TestClass]
public class PptxContentExtractorTests
{
    private PptxContentExtractor _extractor = null!;

    [TestInitialize]
    public void Setup()
    {
        _extractor = new PptxContentExtractor();
    }

    [TestMethod]
    public void CanExtract_PptxMime_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/vnd.openxmlformats-officedocument.presentationml.presentation"));
    }

    [TestMethod]
    public void CanExtract_PptmMime_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/vnd.ms-powerpoint.presentation.macroEnabled.12"));
    }

    [TestMethod]
    public void CanExtract_PotxMime_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/vnd.openxmlformats-officedocument.presentationml.template"));
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("APPLICATION/VND.OPENXMLFORMATS-OFFICEDOCUMENT.PRESENTATIONML.PRESENTATION"));
    }

    [TestMethod]
    public void CanExtract_TextPlain_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("text/plain"));
    }

    [TestMethod]
    public void CanExtract_Docx_ReturnsFalse()
    {
        Assert.IsFalse(_extractor.CanExtract("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
    }
}

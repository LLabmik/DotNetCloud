using DotNetCloud.Modules.Search.Extractors;

namespace DotNetCloud.Modules.Search.Tests;

/// <summary>
/// Tests for <see cref="OdfContentExtractor"/>.
/// </summary>
[TestClass]
public class OdfContentExtractorTests
{
    private OdfContentExtractor _extractor = null!;

    [TestInitialize]
    public void Setup()
    {
        _extractor = new OdfContentExtractor();
    }

    [TestMethod]
    public void CanExtract_OdtMime_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/vnd.oasis.opendocument.text"));
    }

    [TestMethod]
    public void CanExtract_OdsMime_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/vnd.oasis.opendocument.spreadsheet"));
    }

    [TestMethod]
    public void CanExtract_OdpMime_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/vnd.oasis.opendocument.presentation"));
    }

    [TestMethod]
    public void CanExtract_OdgMime_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("application/vnd.oasis.opendocument.graphics"));
    }

    [TestMethod]
    public void CanExtract_CaseInsensitive_ReturnsTrue()
    {
        Assert.IsTrue(_extractor.CanExtract("APPLICATION/VND.OASIS.OPENDOCUMENT.TEXT"));
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

    [TestMethod]
    public async Task ExtractAsync_OdtZipWithContent_ExtractsText()
    {
        // Create a minimal ODF ZIP with content.xml
        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("content.xml");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("""
                <?xml version="1.0" encoding="UTF-8"?>
                <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                  <office:body>
                    <office:text>
                      <text:p>Hello from ODF</text:p>
                      <text:p>Second paragraph</text:p>
                    </office:text>
                  </office:body>
                </office:document-content>
                """);
        }

        memoryStream.Position = 0;
        var result = await _extractor.ExtractAsync(memoryStream, "application/vnd.oasis.opendocument.text");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Hello from ODF"));
        Assert.IsTrue(result.Text.Contains("Second paragraph"));
    }

    [TestMethod]
    public async Task ExtractAsync_WithMetaXml_ExtractsMetadata()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var metaEntry = archive.CreateEntry("meta.xml");
            using (var writer = new StreamWriter(metaEntry.Open()))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <office:document-meta xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:meta="urn:oasis:names:tc:opendocument:xmlns:meta:1.0" xmlns:dc="http://purl.org/dc/elements/1.1/">
                      <office:meta>
                        <dc:title>Test Document</dc:title>
                        <dc:creator>Test Author</dc:creator>
                      </office:meta>
                    </office:document-meta>
                    """);
            }

            var contentEntry = archive.CreateEntry("content.xml");
            using (var writer = new StreamWriter(contentEntry.Open()))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                      <office:body>
                        <office:text>
                          <text:p>Content here</text:p>
                        </office:text>
                      </office:body>
                    </office:document-content>
                    """);
            }
        }

        memoryStream.Position = 0;
        var result = await _extractor.ExtractAsync(memoryStream, "application/vnd.oasis.opendocument.text");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Text.Contains("Content here"));
        Assert.AreEqual("Test Document", result.Metadata["title"]);
        Assert.AreEqual("Test Author", result.Metadata["author"]);
    }

    [TestMethod]
    public async Task ExtractAsync_NoContentXml_ReturnsNull()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("mimetype");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("application/vnd.oasis.opendocument.text");
        }

        memoryStream.Position = 0;
        var result = await _extractor.ExtractAsync(memoryStream, "application/vnd.oasis.opendocument.text");

        Assert.IsNull(result);
    }
}

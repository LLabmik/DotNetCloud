using DotNetCloud.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DotNetCloud.Core.Tests.Security;

[TestClass]
public sealed class FileValidationServiceTests
{
    private readonly IFileValidationService _service = new FileValidationService();

    // ── Constructor ────────────────────────────────────────────────────

    [TestMethod]
    public void Constructor_Default_DoesNotThrow()
    {
        var service = new FileValidationService();
        Assert.IsNotNull(service);
    }

    // ── Validate — Null/Empty ──────────────────────────────────────────

    [TestMethod]
    public void Validate_NullFile_ThrowsArgumentNullException()
    {
        try
        {
            _service.Validate(null!, AllowedFileTypes.AvatarTypes);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Validate_NullAllowedTypes_ThrowsArgumentNullException()
    {
        var file = CreateMockFormFile("test.jpg", "image/jpeg");
        try
        {
            _service.Validate(file, null!);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Validate_EmptyAllowedTypes_ReturnsFailure()
    {
        var file = CreateMockFormFile("test.jpg", "image/jpeg");
        var result = _service.Validate(file, []);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_NO_ALLOWED_TYPES", result.ErrorCode);
    }

    [TestMethod]
    public void Validate_EmptyFile_ReturnsFailure()
    {
        var file = CreateMockFormFile("test.jpg", "image/jpeg", contentSize: 0);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_EMPTY", result.ErrorCode);
    }

    // ── Validate — Extension Checking ──────────────────────────────────

    [TestMethod]
    public void Validate_NoExtension_ReturnsFailure()
    {
        var file = CreateMockFormFile("test", "image/jpeg");
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_NO_EXTENSION", result.ErrorCode);
    }

    [TestMethod]
    public void Validate_DisallowedExtension_ReturnsFailure()
    {
        var file = CreateMockFormFile("doc.pdf", "application/pdf");
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_EXTENSION_NOT_ALLOWED", result.ErrorCode);
    }

    [TestMethod]
    public void Validate_AllowedExtension_ReturnsSuccess()
    {
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var file = CreateMockFormFile("avatar.jpg", "image/jpeg", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_CaseInsensitiveExtension_ReturnsSuccess()
    {
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var file = CreateMockFormFile("avatar.JPG", "image/jpeg", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsTrue(result.IsValid);
    }

    // ── Validate — File Size ───────────────────────────────────────────

    [TestMethod]
    public void Validate_FileExceedsMaxSize_ReturnsFailure()
    {
        // AvatarTypes have a max of 5 MB; create a file larger than that
        var file = CreateMockFormFile("large.jpg", "image/jpeg", contentSize: 10 * 1024 * 1024);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_TOO_LARGE", result.ErrorCode);
    }

    [TestMethod]
    public void Validate_FileWithinMaxSize_ReturnsSuccess()
    {
        var content = new byte[1024];
        content[0] = 0xFF;
        content[1] = 0xD8;
        content[2] = 0xFF;
        var file = CreateMockFormFile("avatar.jpg", "image/jpeg", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_CustomMaxSizeOverride_ReturnsFailure()
    {
        var file = CreateMockFormFile("file.jpg", "image/jpeg", contentSize: 200);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes, maxSize: 100);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_TOO_LARGE", result.ErrorCode);
    }

    // ── Validate — Magic Bytes ─────────────────────────────────────────

    [TestMethod]
    public void Validate_ValidJpegMagicBytes_ReturnsSuccess()
    {
        // JPEG files start with 0xFF 0xD8 0xFF
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var file = CreateMockFormFile("photo.jpg", "image/jpeg", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_ValidPngMagicBytes_ReturnsSuccess()
    {
        // PNG files start with 0x89 0x50 0x4E 0x47 0x0D 0x0A 0x1A 0x0A
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        var file = CreateMockFormFile("image.png", "image/png", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_ValidGifMagicBytes_ReturnsSuccess()
    {
        // GIF89a header
        var content = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
        var file = CreateMockFormFile("animation.gif", "image/gif", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_ValidWebpMagicBytes_ReturnsSuccess()
    {
        // WebP starts with RIFF
        var content = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        var file = CreateMockFormFile("image.webp", "image/webp", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_FileContentDoesNotMatchExtension_ReturnsFailure()
    {
        // PNG magic bytes but .jpg extension
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var file = CreateMockFormFile("fake.jpg", "image/jpeg", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_CONTENT_MISMATCH", result.ErrorCode);
    }

    [TestMethod]
    public void Validate_ExeRenamedToJpg_ReturnsFailure()
    {
        // An actual executable would start with MZ (0x4D 0x5A)
        var content = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        var file = CreateMockFormFile("malware.jpg", "image/jpeg", content);
        var result = _service.Validate(file, AllowedFileTypes.AvatarTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_CONTENT_MISMATCH", result.ErrorCode);
    }

    // ── Validate — CSV (no magic bytes) ────────────────────────────────

    [TestMethod]
    public void Validate_CsvFile_ReturnsSuccess()
    {
        var content = "name,email,phone\r\nAlice,alice@example.com,555-0100"u8.ToArray();
        var file = CreateMockFormFile("contacts.csv", "text/csv", content);
        var result = _service.Validate(file, AllowedFileTypes.CsvImportTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_NonCsvUploadedToCsvEndpoint_ReturnsFailure()
    {
        var file = CreateMockFormFile("data.txt", "text/plain", contentSize: 100);
        var result = _service.Validate(file, AllowedFileTypes.CsvImportTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_EXTENSION_NOT_ALLOWED", result.ErrorCode);
    }

    // ── Validate — HTML (bookmarks import) ─────────────────────────────

    [TestMethod]
    public void Validate_HtmlFile_ReturnsSuccess()
    {
        var content = "<!DOCTYPE html><html><head><title>Bookmarks</title></head></html>"u8.ToArray();
        var file = CreateMockFormFile("bookmarks.html", "text/html", content);
        var result = _service.Validate(file, AllowedFileTypes.BookmarkImportTypes);
        Assert.IsTrue(result.IsValid);
    }

    // ── Validate — Email attachments ───────────────────────────────────

    [TestMethod]
    public void Validate_EmailAttachmentPdf_ReturnsSuccess()
    {
        var content = "%PDF-1.4"u8.ToArray();
        var file = CreateMockFormFile("document.pdf", "application/pdf", content);
        var result = _service.Validate(file, AllowedFileTypes.EmailAttachmentTypes);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_EmailAttachmentScript_ReturnsFailure()
    {
        var file = CreateMockFormFile("script.exe", "application/x-msdownload", contentSize: 100);
        var result = _service.Validate(file, AllowedFileTypes.EmailAttachmentTypes);
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("FILE_EXTENSION_NOT_ALLOWED", result.ErrorCode);
    }

    // ── SanitizeFileName ───────────────────────────────────────────────

    [TestMethod]
    public void SanitizeFileName_Null_ReturnsUntitled()
    {
        var result = _service.SanitizeFileName(null!);
        Assert.AreEqual("untitled", result);
    }

    [TestMethod]
    public void SanitizeFileName_Empty_ReturnsUntitled()
    {
        var result = _service.SanitizeFileName("");
        Assert.AreEqual("untitled", result);
    }

    [TestMethod]
    public void SanitizeFileName_Whitespace_ReturnsUntitled()
    {
        var result = _service.SanitizeFileName("   ");
        Assert.AreEqual("untitled", result);
    }

    [TestMethod]
    public void SanitizeFileName_StripsPathTraversal()
    {
        var result = _service.SanitizeFileName("../../etc/passwd");
        Assert.IsFalse(result.Contains(".."), "Path traversal not stripped");
        Assert.IsFalse(result.Contains("/"), "Forward slash not stripped");
    }

    [TestMethod]
    public void SanitizeFileName_StripsWindowsPath()
    {
        var result = _service.SanitizeFileName("..\\..\\Windows\\system32\\evil.exe");
        Assert.IsFalse(result.Contains(".."), "Path traversal not stripped");
        Assert.IsFalse(result.Contains("\\"), "Backslash not stripped");
    }

    [TestMethod]
    public void SanitizeFileName_StripsNullBytes()
    {
        var result = _service.SanitizeFileName("file.txt\0.exe");
        Assert.IsFalse(result.Contains('\0'), "Null byte not stripped");
    }

    [TestMethod]
    public void SanitizeFileName_StripsDangerousChars()
    {
        var result = _service.SanitizeFileName("file:*?\"<>|.txt");
        Assert.AreEqual("file.txt", result, "Dangerous characters not stripped");
    }

    [TestMethod]
    public void SanitizeFileName_NormalFileName_Unchanged()
    {
        var result = _service.SanitizeFileName("my_document.pdf");
        Assert.AreEqual("my_document.pdf", result);
    }

    [TestMethod]
    public void SanitizeFileName_VeryLongName_Truncated()
    {
        var longName = new string('a', 300) + ".txt";
        var result = _service.SanitizeFileName(longName);
        Assert.IsTrue(result.Length <= 255, "Filename not truncated to 255 chars");
        Assert.IsTrue(result.EndsWith(".txt"), "Extension preserved");
    }

    [TestMethod]
    public void SanitizeFileName_SanitizeToNothing_ReturnsUntitled()
    {
        var result = _service.SanitizeFileName("..\\..\\..");
        Assert.AreEqual("untitled", result);
    }

    // ── Additional security edge cases ────────────────────────────────

    [TestMethod]
    public void SanitizeFileName_AbsolutePath_Stripped()
    {
        var result = _service.SanitizeFileName("/etc/shadow");
        Assert.IsFalse(result.Contains("/"), "Leading slash not stripped");
    }

    [TestMethod]
    public void SanitizeFileName_WindowsAbsolutePath_Stripped()
    {
        var result = _service.SanitizeFileName("C:\\Windows\\system32\\evil.dll");
        Assert.IsFalse(result.Contains(":"), "Colon not stripped");
        Assert.IsFalse(result.Contains("\\"), "Backslash not stripped");
    }

    // ── AllowedFileTypes integrity ────────────────────────────────────

    [TestMethod]
    public void AllowedFileTypes_AvatarTypes_HaveMagicBytes()
    {
        foreach (var type in AllowedFileTypes.AvatarTypes)
        {
            Assert.IsTrue(type.MagicBytes.Length > 0,
                $"Avatar type {string.Join(", ", type.Extensions)} has no magic bytes defined");
        }
    }

    [TestMethod]
    public void AllowedFileTypes_AvatarTypes_HaveSizeLimit()
    {
        foreach (var type in AllowedFileTypes.AvatarTypes)
        {
            Assert.IsNotNull(type.MaxSize,
                $"Avatar type {string.Join(", ", type.Extensions)} has no size limit");
        }
    }

    [TestMethod]
    public void AllowedFileTypes_AvatarTypes_NoDuplicateExtensions()
    {
        var allExtensions = AllowedFileTypes.AvatarTypes
            .SelectMany(t => t.Extensions)
            .Select(e => e.ToLowerInvariant())
            .ToArray();
        Assert.AreEqual(allExtensions.Length, allExtensions.Distinct().Count(),
            "Duplicate extensions found in AvatarTypes");
    }

    [TestMethod]
    public void AllowedFileTypes_EmailAttachmentTypes_HasSizeLimit()
    {
        Assert.IsNotNull(AllowedFileTypes.EmailAttachmentTypes[0].MaxSize,
            "Email attachment type has no size limit");
    }

    [TestMethod]
    public void AllowedFileTypes_CsvImportTypes_HasSizeLimit()
    {
        Assert.IsNotNull(AllowedFileTypes.CsvImportTypes[0].MaxSize,
            "CSV import type has no size limit");
    }

    [TestMethod]
    public void AllowedFileTypes_BookmarkImportTypes_HasSizeLimit()
    {
        Assert.IsNotNull(AllowedFileTypes.BookmarkImportTypes[0].MaxSize,
            "Bookmark import type has no size limit");
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static IFormFile CreateMockFormFile(string fileName, string contentType, byte[]? content = null, int? contentSize = null)
    {
        byte[] bytes;
        if (content is not null)
        {
            bytes = content;
        }
        else
        {
            bytes = new byte[contentSize ?? 1024];
            // Fill with random-ish bytes so magic byte checks fail if expected
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i % 256);
            }
        }

        var stream = new MemoryStream(bytes);
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        formFile.Setup(f => f.Length).Returns(bytes.Length);
        formFile.Setup(f => f.OpenReadStream()).Returns(stream);
        formFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>(async (s, _) =>
            {
                stream.Position = 0;
                await stream.CopyToAsync(s);
            })
            .Returns(Task.CompletedTask);
        formFile.Setup(f => f.CopyTo(It.IsAny<Stream>()))
            .Callback<Stream>(s =>
            {
                stream.Position = 0;
                stream.CopyTo(s);
            });

        return formFile.Object;
    }
}

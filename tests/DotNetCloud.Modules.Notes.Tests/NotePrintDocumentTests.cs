using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Notes.UI;
using DotNetCloud.UI.Shared.Services;

namespace DotNetCloud.Modules.Notes.Tests;

/// <summary>
/// Unit tests for <see cref="NotePrintDocument"/> — the payload handed to the browser's
/// print helper must contain the note's title and its formatted (sanitized) content.
/// </summary>
[TestClass]
public class NotePrintDocumentTests
{
    private readonly IMarkdownRenderer _renderer = new MarkdownRenderer();

    [TestMethod]
    public void Create_MarkdownNote_RendersFormattedHtml()
    {
        var note = MakeNote("Release notes", "# Heading\n\n- one\n- two\n\n**bold** text");

        var payload = NotePrintDocument.Create(note, _renderer);

        Assert.AreEqual("Release notes", payload.Title);
        StringAssert.Contains(payload.Html, "Heading</h1>");
        StringAssert.Contains(payload.Html, "<li>one</li>");
        StringAssert.Contains(payload.Html, "<strong>bold</strong>");
    }

    [TestMethod]
    public void Create_BlankTitle_UsesFallbackTitle()
    {
        var note = MakeNote("   ", "body");

        var payload = NotePrintDocument.Create(note, _renderer);

        Assert.AreEqual(NotePrintDocument.FallbackTitle, payload.Title);
    }

    [TestMethod]
    public void Create_TitleWithSurroundingWhitespace_IsTrimmed()
    {
        var note = MakeNote("  Shopping list  ", "- milk");

        var payload = NotePrintDocument.Create(note, _renderer);

        Assert.AreEqual("Shopping list", payload.Title);
    }

    [TestMethod]
    public void Create_EmptyContent_ProducesEmptyHtml()
    {
        var note = MakeNote("Empty note", string.Empty);

        var payload = NotePrintDocument.Create(note, _renderer);

        Assert.AreEqual(string.Empty, payload.Html);
    }

    [TestMethod]
    public void Create_PlainTextNote_PreservesTextAndEscapesMarkup()
    {
        var note = new NoteDto
        {
            Id = Guid.CreateVersion7(),
            OwnerId = Guid.CreateVersion7(),
            Title = "Plain",
            Content = "line one\n<b>not bold</b>",
            Format = NoteContentFormat.PlainText,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var payload = NotePrintDocument.Create(note, _renderer);

        StringAssert.Contains(payload.Html, "print-plaintext");
        StringAssert.Contains(payload.Html, "line one");
        StringAssert.Contains(payload.Html, "&lt;b&gt;not bold&lt;/b&gt;");
        Assert.IsFalse(payload.Html.Contains("<b>not bold</b>", StringComparison.Ordinal),
            "Plain-text notes must not be interpreted as HTML.");
    }

    [TestMethod]
    public void Create_MarkdownWithScriptTag_SanitizesScript()
    {
        var note = MakeNote("Unsafe", "hello\n\n<script>alert('x')</script>");

        var payload = NotePrintDocument.Create(note, _renderer);

        Assert.IsFalse(payload.Html.Contains("<script", StringComparison.OrdinalIgnoreCase),
            "Scripts must be stripped before the HTML reaches the print frame.");
    }

    [TestMethod]
    public void Create_NullRenderer_Throws()
    {
        var note = MakeNote("Title", "body");

        Assert.ThrowsExactly<ArgumentNullException>(() => NotePrintDocument.Create(note, null!));
    }

    private static NoteDto MakeNote(string title, string content) => new()
    {
        Id = Guid.CreateVersion7(),
        OwnerId = Guid.CreateVersion7(),
        Title = title,
        Content = content,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

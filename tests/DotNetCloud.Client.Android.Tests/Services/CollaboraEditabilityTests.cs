using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class CollaboraEditabilityTests
{
    /// <summary>A representative WOPI discovery result: office formats plus native-editor types.</summary>
    private static readonly string[] Discovery =
        ["doc", "docx", "odt", "ods", "odp", "pdf", "pptx", "xlsx", "txt", "csv", "json"];

    // ── IsEditable ───────────────────────────────────────────────────

    [TestMethod]
    public void IsEditable_ExtensionReportedByDiscovery_ReturnsTrue()
    {
        Assert.IsTrue(CollaboraEditability.IsEditable("Report.docx", Discovery));
        Assert.IsTrue(CollaboraEditability.IsEditable("Budget.XLSX", Discovery));
        Assert.IsTrue(CollaboraEditability.IsEditable("legacy.doc", Discovery));
    }

    [TestMethod]
    public void IsEditable_ExtensionNotReportedByDiscovery_ReturnsFalse()
        => Assert.IsFalse(CollaboraEditability.IsEditable("photo.png", Discovery));

    [TestMethod]
    public void IsEditable_NativeEditorExtension_ReturnsFalse()
    {
        // Mirrors the Blazor web UI's NativeEditorExtensions exclusion.
        Assert.IsFalse(CollaboraEditability.IsEditable("notes.txt", Discovery));
        Assert.IsFalse(CollaboraEditability.IsEditable("data.csv", Discovery));
        Assert.IsFalse(CollaboraEditability.IsEditable("settings.json", Discovery));
    }

    [TestMethod]
    public void IsEditable_NoDiscoveryResult_ReturnsFalse()
    {
        Assert.IsFalse(CollaboraEditability.IsEditable("Report.docx", []));
        Assert.IsFalse(CollaboraEditability.IsEditable("Report.docx", null));
        Assert.IsFalse(CollaboraEditability.IsEditable(null, Discovery));
        Assert.IsFalse(CollaboraEditability.IsEditable(string.Empty, Discovery));
    }

    [TestMethod]
    public void IsEditable_ExtensionlessFileName_ReturnsFalse()
        => Assert.IsFalse(CollaboraEditability.IsEditable("README", Discovery));

    // ── CreatableExtensions ──────────────────────────────────────────

    [TestMethod]
    public void CreatableExtensions_PreferredOrderFirst_ThenRemainingSorted()
    {
        var creatable = CollaboraEditability.CreatableExtensions(Discovery);

        CollectionAssert.AreEqual(
            new[] { "docx", "xlsx", "pptx", "odt", "ods", "odp", "doc", "pdf" },
            creatable.ToArray());
    }

    [TestMethod]
    public void CreatableExtensions_OnlyPreferredFormatsReported_ReturnsThoseInOrder()
    {
        var creatable = CollaboraEditability.CreatableExtensions(["odp", "docx", "ods"]);

        CollectionAssert.AreEqual(new[] { "docx", "ods", "odp" }, creatable.ToArray());
    }

    [TestMethod]
    public void CreatableExtensions_NoDiscoveryResult_ReturnsEmpty()
    {
        Assert.AreEqual(0, CollaboraEditability.CreatableExtensions([]).Count);
        Assert.AreEqual(0, CollaboraEditability.CreatableExtensions(null).Count);
    }

    [TestMethod]
    public void CreatableExtensions_OnlyNativeExtensionsReported_ReturnsEmpty()
        => Assert.AreEqual(0, CollaboraEditability.CreatableExtensions(["txt", "csv", "json"]).Count);

    // ── NormalizeExtension ───────────────────────────────────────────

    [TestMethod]
    public void NormalizeExtension_StripsDotAndLowercases()
    {
        Assert.AreEqual("docx", CollaboraEditability.NormalizeExtension(".DOCX"));
        Assert.AreEqual("docx", CollaboraEditability.NormalizeExtension(" docx "));
        Assert.AreEqual(string.Empty, CollaboraEditability.NormalizeExtension(null));
        Assert.AreEqual(string.Empty, CollaboraEditability.NormalizeExtension("   "));
    }

    // ── MimeTypeFor ──────────────────────────────────────────────────

    [TestMethod]
    public void MimeTypeFor_KnownOfficeExtensions_ReturnsOfficeMimeTypes()
    {
        Assert.AreEqual(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            CollaboraEditability.MimeTypeFor("docx"));
        Assert.AreEqual(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            CollaboraEditability.MimeTypeFor(".xlsx"));
        Assert.AreEqual(
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            CollaboraEditability.MimeTypeFor("PPTX"));
        Assert.AreEqual("application/vnd.oasis.opendocument.text", CollaboraEditability.MimeTypeFor("odt"));
        Assert.AreEqual("application/vnd.oasis.opendocument.spreadsheet", CollaboraEditability.MimeTypeFor("ods"));
        Assert.AreEqual("application/vnd.oasis.opendocument.presentation", CollaboraEditability.MimeTypeFor("odp"));
    }

    [TestMethod]
    public void MimeTypeFor_TextExtensions_ReturnsTextMimeTypes()
    {
        Assert.AreEqual("text/plain", CollaboraEditability.MimeTypeFor("txt"));
        Assert.AreEqual("text/markdown", CollaboraEditability.MimeTypeFor("md"));
        Assert.AreEqual("text/csv", CollaboraEditability.MimeTypeFor("csv"));
    }

    [TestMethod]
    public void MimeTypeFor_UnknownExtension_FallsBackToPlainText()
        => Assert.AreEqual("text/plain", CollaboraEditability.MimeTypeFor("wpd"));
}

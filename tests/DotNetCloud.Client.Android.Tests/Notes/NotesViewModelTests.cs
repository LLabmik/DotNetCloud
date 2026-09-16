using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Notes;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.Android.Tests.Notes;

[TestClass]
public sealed class NotesViewModelTests
{
    private static NotesViewModel CreateViewModel()
    {
        return new NotesViewModel(
            new Mock<INotesRestClient>(MockBehavior.Loose).Object,
            new Mock<IOfflineOperationQueue>(MockBehavior.Loose).Object,
            new Mock<IConnectivityMonitor>(MockBehavior.Loose).Object,
            new Mock<IServerConnectionStore>(MockBehavior.Loose).Object,
            new Mock<ISecureTokenStore>(MockBehavior.Loose).Object,
            NullLogger<NotesViewModel>.Instance);
    }

    // ── System back (Android hardware button / predictive-back gesture) ─

    [TestMethod]
    public void CanHandleSystemBack_WithoutPreview_IsFalse()
    {
        var vm = CreateViewModel();

        Assert.IsFalse(vm.CanHandleSystemBack);
    }

    [TestMethod]
    public void CanHandleSystemBack_WithPreviewOpen_IsTrue()
    {
        var vm = CreateViewModel();
        vm.IsPreviewVisible = true;

        Assert.IsTrue(vm.CanHandleSystemBack);
    }

    [TestMethod]
    public async Task HandleSystemBackAsync_WithPreviewOpen_ClosesPreview()
    {
        var vm = CreateViewModel();
        vm.IsPreviewVisible = true;
        vm.PreviewHtml = "<p>note</p>";

        var handled = await vm.HandleSystemBackAsync();

        Assert.IsTrue(handled);
        Assert.IsFalse(vm.IsPreviewVisible);
        Assert.IsNull(vm.SelectedNote);
        Assert.AreEqual(string.Empty, vm.PreviewHtml);
    }

    [TestMethod]
    public async Task HandleSystemBackAsync_WithoutPreview_ReturnsFalse()
    {
        var vm = CreateViewModel();

        var handled = await vm.HandleSystemBackAsync();

        Assert.IsFalse(handled);
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_WrapsInFullHtmlDocument()
    {
        var input = "<p>Hello world</p>";
        var result = NotesViewModel.WrapHtmlWithDarkTheme(input);

        Assert.IsTrue(result.StartsWith("<!DOCTYPE html>"), "Should start with DOCTYPE");
        Assert.IsTrue(result.Contains("<html>"), "Should contain html tag");
        Assert.IsTrue(result.Contains("<head>"), "Should contain head tag");
        Assert.IsTrue(result.Contains("<style>"), "Should contain style tag");
        Assert.IsTrue(result.Contains("</style>"), "Should close style tag");
        Assert.IsTrue(result.Contains("<body>"), "Should contain body tag");
        Assert.IsTrue(result.Contains("</body>"), "Should close body tag");
        Assert.IsTrue(result.Contains("</html>"), "Should close html tag");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_IncludesInputHtml()
    {
        var input = "<p>Hello world</p>";
        var result = NotesViewModel.WrapHtmlWithDarkTheme(input);

        Assert.IsTrue(result.Contains(input), "Should contain original HTML");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasDarkThemeCss()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("#0F172A"), "Should have dark background color");
        Assert.IsTrue(result.Contains("#E2E8F0"), "Should have light text color");
        Assert.IsTrue(result.Contains("!important"), "Should use !important overrides");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HandlesEmptyHtml()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme(string.Empty);

        Assert.IsTrue(result.Contains("<body></body>"), "Should handle empty body");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasColorSchemeDark()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("color-scheme: dark"), "Should have dark color-scheme");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasViewportMeta()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("viewport"), "Should have viewport meta tag");
        Assert.IsTrue(result.Contains("width=device-width"), "Should have responsive viewport");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasLinkStyles()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("a {"), "Should have link styles");
        Assert.IsTrue(result.Contains("#38BDF8"), "Should have light blue link color");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasCodeStyles()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("code {"), "Should have code styles");
        Assert.IsTrue(result.Contains("#F472B6"), "Should have pink code color");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasTableStyles()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("table {"), "Should have table styles");
        Assert.IsTrue(result.Contains("border-collapse"), "Should collapse table borders");
    }

    [TestMethod]
    public void WrapHtmlWithDarkTheme_HasParagraphStyle()
    {
        var result = NotesViewModel.WrapHtmlWithDarkTheme("<p>test</p>");

        Assert.IsTrue(result.Contains("p {"), "Should have paragraph style");
        Assert.IsTrue(result.Contains("#E2E8F0"), "Should have light paragraph text");
    }

    // ── Folder filter chips ─────────────────────────────────────────

    [TestMethod]
    public void FolderChips_WithoutFolders_ContainOnlyTheAllNotesChip()
    {
        var vm = CreateViewModel();

        Assert.AreEqual(1, vm.FolderChips.Count);
        Assert.IsNull(vm.FolderChips[0].Id);
        Assert.AreEqual(NotesViewModel.AllNotesLabel, vm.FolderChips[0].Name);
    }

    [TestMethod]
    public void FolderChips_WhenFoldersAppear_MirrorThemAfterAllNotes()
    {
        var vm = CreateViewModel();
        var finance = Guid.NewGuid();
        var work = Guid.NewGuid();

        vm.Folders.Add(CreateFolder(finance, "Finance"));
        vm.Folders.Add(CreateFolder(work, "Work"));

        Assert.AreEqual(3, vm.FolderChips.Count);
        Assert.IsNull(vm.FolderChips[0].Id, "All Notes must stay first so the filter can always be cleared");
        Assert.AreEqual(finance, vm.FolderChips[1].Id);
        Assert.AreEqual("Finance", vm.FolderChips[1].Name);
        Assert.AreEqual(work, vm.FolderChips[2].Id);
        Assert.AreEqual("Work", vm.FolderChips[2].Name);
    }

    [TestMethod]
    public void FolderChips_WhenFolderRemoved_DropIt()
    {
        var vm = CreateViewModel();
        var finance = Guid.NewGuid();
        vm.Folders.Add(CreateFolder(finance, "Finance"));

        vm.Folders.RemoveAt(0);

        Assert.AreEqual(1, vm.FolderChips.Count);
        Assert.IsNull(vm.FolderChips[0].Id);
    }

    [TestMethod]
    public void FolderChips_RenamedFolder_ShowsTheNewLabel()
    {
        var vm = CreateViewModel();
        var finance = Guid.NewGuid();
        vm.Folders.Add(CreateFolder(finance, "Finance"));

        vm.Folders[0] = vm.Folders[0] with { Name = "Money" };

        Assert.AreEqual("Money", vm.FolderChips[1].Name);
    }

    private static NoteFolderDto CreateFolder(Guid id, string name) => new()
    {
        Id = id,
        OwnerId = Guid.NewGuid(),
        Name = name,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}

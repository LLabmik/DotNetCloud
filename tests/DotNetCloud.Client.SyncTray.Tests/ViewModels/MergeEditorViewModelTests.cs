using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class MergeEditorViewModelTests
{
    private string _tempDir = null!;
    private string _localFile = null!;
    private string _serverFile = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DncMergeTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _localFile = Path.Combine(_tempDir, "file (conflict - user - 2026-03-09).txt");
        _serverFile = Path.Combine(_tempDir, "file.txt");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    // ── Initial state ─────────────────────────────────────────────────────

    [TestMethod]
    public void NewViewModel_HasEmptyContent()
    {
        var vm = new MergeEditorViewModel(NullLogger.Instance);
        Assert.AreEqual(string.Empty, vm.MergedContent);
        Assert.AreEqual(string.Empty, vm.FileName);
        Assert.IsFalse(vm.IsBinary);
    }

    // ── Text file merge ───────────────────────────────────────────────────

    [TestMethod]
    public void LoadConflict_TextFile_PopulatesDiffLines()
    {
        File.WriteAllText(_serverFile, "line1\nline2\nline3");
        File.WriteAllText(_localFile, "line1\nmodified\nline3");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        Assert.IsTrue(vm.LocalLines.Count > 0, "Local lines should be populated.");
        Assert.IsTrue(vm.ServerLines.Count > 0, "Server lines should be populated.");
        Assert.IsFalse(string.IsNullOrEmpty(vm.MergedContent), "Merged content should be generated.");
        Assert.AreEqual("file.txt", vm.FileName);
    }

    [TestMethod]
    public void LoadConflict_IdenticalContent_NoConflicts()
    {
        File.WriteAllText(_serverFile, "same content\nline2");
        File.WriteAllText(_localFile, "same content\nline2");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        Assert.AreEqual(0, vm.ConflictCount);
        Assert.IsFalse(vm.HasConflicts);
    }

    [TestMethod]
    public void LoadConflict_DifferentLines_DetectsChanges()
    {
        File.WriteAllText(_serverFile, "line1\nserver-change\nline3");
        File.WriteAllText(_localFile, "line1\nlocal-change\nline3");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        Assert.IsTrue(vm.LocalChangeCount > 0 || vm.ServerChangeCount > 0,
            "Should detect changes between local and server.");
    }

    [TestMethod]
    public void LoadConflict_ConflictingChanges_InsertsConflictMarkers()
    {
        File.WriteAllText(_serverFile, "line1\nserver-version\nline3");
        File.WriteAllText(_localFile, "line1\nlocal-version\nline3");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        Assert.IsTrue(vm.HasConflicts, "Conflicting changes should be detected.");
        Assert.IsTrue(vm.ConflictCount > 0);
        StringAssert.Contains(vm.MergedContent, "<<<<<<< LOCAL");
        StringAssert.Contains(vm.MergedContent, "=======");
        StringAssert.Contains(vm.MergedContent, ">>>>>>> SERVER");
    }

    // ── Binary file handling ──────────────────────────────────────────────

    [TestMethod]
    public void LoadConflict_BinaryFile_SetsIsBinary()
    {
        var binaryServer = Path.Combine(_tempDir, "image.png");
        var binaryLocal = Path.Combine(_tempDir, "image (conflict).png");
        File.WriteAllBytes(binaryServer, [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllBytes(binaryLocal, [0x89, 0x50, 0x4E, 0x47]);

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(binaryServer, binaryLocal);

        Assert.IsTrue(vm.IsBinary, "PNG files should be classified as binary.");
        Assert.AreEqual(string.Empty, vm.MergedContent, "Binary files should not generate merged content.");
    }

    // ── XML file detection ────────────────────────────────────────────────

    [TestMethod]
    public void LoadConflict_XmlFile_SetsIsXml()
    {
        var xmlServer = Path.Combine(_tempDir, "config.xml");
        var xmlLocal = Path.Combine(_tempDir, "config (conflict).xml");
        File.WriteAllText(xmlServer, "<root><item>server</item></root>");
        File.WriteAllText(xmlLocal, "<root><item>local</item></root>");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(xmlServer, xmlLocal);

        Assert.IsTrue(vm.IsXml, "XML files should be detected.");
        Assert.IsFalse(vm.IsBinary, "XML files should not be binary.");
    }

    // ── Accept All Local ──────────────────────────────────────────────────

    [TestMethod]
    public void AcceptAllLocal_SetsMergedToLocalContent()
    {
        File.WriteAllText(_serverFile, "server content");
        File.WriteAllText(_localFile, "local content");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        vm.AcceptAllLocalCommand.Execute(null);

        Assert.AreEqual("local content", vm.MergedContent);
    }

    // ── Accept All Server ─────────────────────────────────────────────────

    [TestMethod]
    public void AcceptAllServer_SetsMergedToServerContent()
    {
        File.WriteAllText(_serverFile, "server content");
        File.WriteAllText(_localFile, "local content");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        vm.AcceptAllServerCommand.Execute(null);

        Assert.AreEqual("server content", vm.MergedContent);
    }

    // ── Reset Merge ───────────────────────────────────────────────────────

    [TestMethod]
    public void ResetMerge_RestoresAutoMergedContent()
    {
        File.WriteAllText(_serverFile, "line1\nline2");
        File.WriteAllText(_localFile, "line1\nline2");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        var originalMerged = vm.MergedContent;
        vm.MergedContent = "user edited this";

        vm.ResetMergeCommand.Execute(null);

        Assert.AreEqual(originalMerged, vm.MergedContent);
    }

    // ── Save and Resolve ──────────────────────────────────────────────────

    [TestMethod]
    public void SaveAndResolve_InvokesCallbackWithMergedContent()
    {
        File.WriteAllText(_serverFile, "server");
        File.WriteAllText(_localFile, "local");

        string? result = null;
        var vm = new MergeEditorViewModel(NullLogger.Instance, mergedContent => result = mergedContent);
        vm.LoadConflict(_serverFile, _localFile);

        vm.MergedContent = "final merged result";
        vm.SaveAndResolveCommand.Execute(null);

        Assert.AreEqual("final merged result", result);
    }

    [TestMethod]
    public void SaveAndResolve_RaisesCloseRequested()
    {
        File.WriteAllText(_serverFile, "server");
        File.WriteAllText(_localFile, "local");

        var closed = false;
        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);
        vm.CloseRequested += (_, _) => closed = true;

        vm.SaveAndResolveCommand.Execute(null);

        Assert.IsTrue(closed, "CloseRequested should be raised on save.");
    }

    // ── Cancel ────────────────────────────────────────────────────────────

    [TestMethod]
    public void Cancel_InvokesCallbackWithNull()
    {
        string? result = "not-null";
        var vm = new MergeEditorViewModel(NullLogger.Instance, mergedContent => result = mergedContent);

        vm.CancelCommand.Execute(null);

        Assert.IsNull(result, "Cancel should pass null to the callback.");
    }

    [TestMethod]
    public void Cancel_RaisesCloseRequested()
    {
        var closed = false;
        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.CloseRequested += (_, _) => closed = true;

        vm.CancelCommand.Execute(null);

        Assert.IsTrue(closed, "CloseRequested should be raised on cancel.");
    }

    // ── Status text ───────────────────────────────────────────────────────

    [TestMethod]
    public void LoadConflict_SetsStatusText()
    {
        File.WriteAllText(_serverFile, "a");
        File.WriteAllText(_localFile, "b");

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        Assert.IsFalse(string.IsNullOrEmpty(vm.StatusText), "Status text should be set after loading.");
    }

    // ── Missing files ─────────────────────────────────────────────────────

    [TestMethod]
    public void LoadConflict_MissingLocalFile_HandlesGracefully()
    {
        File.WriteAllText(_serverFile, "server content");
        // _localFile does not exist

        var vm = new MergeEditorViewModel(NullLogger.Instance);
        vm.LoadConflict(_serverFile, _localFile);

        // Should not throw; local content treated as empty.
        Assert.IsNotNull(vm.MergedContent);
    }
}

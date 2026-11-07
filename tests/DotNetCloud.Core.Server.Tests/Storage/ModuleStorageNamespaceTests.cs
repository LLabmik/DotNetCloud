using DotNetCloud.Core.Server.Storage;

namespace DotNetCloud.Core.Server.Tests.Storage;

/// <summary>
/// Tests for <see cref="ModuleStorageNamespace"/>: every object a module addresses resolves inside
/// its own (or its acting user's) namespace, and hostile paths are rejected before any I/O (F5).
/// </summary>
[TestClass]
public class ModuleStorageNamespaceTests
{
    private const string ModuleId = "org.example.customapp";
    private const string OtherModuleId = "dotnetcloud.files";

    private string _root = null!;

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), $"dnc-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string RelativeTo(string root, string path) =>
        Path.GetRelativePath(Path.GetFullPath(root), path).Replace('\\', '/');

    [TestMethod]
    public void TryBuild_ModuleScope_PlacesObjectUnderModuleRoot()
    {
        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "attachments", userId: null, "2026/report.txt", out var path, out var error);

        Assert.IsTrue(built, error);
        Assert.AreEqual($"{ModuleId}/attachments/2026/report.txt", RelativeTo(_root, path));
    }

    [TestMethod]
    public void TryBuild_UserScope_InsertsTheVerifiedUserSegment()
    {
        var userId = Guid.NewGuid();

        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "attachments", userId.ToString(), "a/b.txt", out var path, out var error);

        Assert.IsTrue(built, error);
        Assert.AreEqual(
            $"{ModuleId}/users/{userId:D}/attachments/a/b.txt",
            RelativeTo(_root, path));
    }

    [TestMethod]
    public void TryBuild_UserScope_NormalizesTheUserIdSpelling()
    {
        var userId = Guid.NewGuid();

        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "notes", userId.ToString("B"), "x.txt", out var path, out var error);

        Assert.IsTrue(built, error);
        Assert.IsTrue(path.Contains($"/users/{userId:D}/", StringComparison.Ordinal),
            "The namespace segment must not depend on the caller's spelling of the id");
    }

    [TestMethod]
    public void TryBuild_EmptyPath_AddressesTheContainer()
    {
        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "attachments", userId: null, string.Empty, out var path, out var error);

        Assert.IsTrue(built, error);
        Assert.AreEqual($"{ModuleId}/attachments", RelativeTo(_root, path));
    }

    [TestMethod]
    [DataRow("../secret.txt")]
    [DataRow("a/../../secret.txt")]
    [DataRow("..")]
    [DataRow("a/..")]
    [DataRow("./a")]
    [DataRow("a//b")]
    [DataRow("/etc/passwd")]
    [DataRow("C:/Windows/System32/drivers/etc/hosts")]
    [DataRow(@"a\b.txt")]
    [DataRow("a\u0000b")]
    public void TryBuild_HostilePaths_AreRejected(string relativePath)
    {
        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "attachments", userId: null, relativePath, out var path, out var error);

        Assert.IsFalse(built, $"Path '{relativePath}' must be rejected");
        Assert.AreEqual(string.Empty, path);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryBuild_TooLongPath_IsRejected()
    {
        var longPath = string.Join('/', Enumerable.Repeat("segment", 200));

        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "attachments", userId: null, longPath, out _, out var error);

        Assert.IsFalse(built);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("..")]
    [DataRow(".")]
    [DataRow("Attachments")]
    [DataRow("../attachments")]
    [DataRow("attachments/child")]
    public void TryBuild_InvalidContainer_IsRejected(string container)
    {
        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, container, userId: null, "a.txt", out _, out var error);

        Assert.IsFalse(built);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("../evil")]
    [DataRow("Org.Example.App")]
    [DataRow("-leading")]
    [DataRow("org.example/app")]
    public void TryBuild_InvalidModuleId_IsRejected(string moduleId)
    {
        var built = ModuleStorageNamespace.TryBuild(
            _root, moduleId, "attachments", userId: null, "a.txt", out _, out var error);

        Assert.IsFalse(built);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryBuild_InvalidUserId_IsRejected()
    {
        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "attachments", "not-a-guid", "a.txt", out _, out var error);

        Assert.IsFalse(built);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryBuild_AnotherModuleIdInThePath_StaysInsideTheCallersNamespace()
    {
        var built = ModuleStorageNamespace.TryBuild(
            _root, ModuleId, "attachments", userId: null, $"{OtherModuleId}/secret.txt", out var path, out var error);

        Assert.IsTrue(built, error);
        Assert.AreEqual(
            $"{ModuleId}/attachments/{OtherModuleId}/secret.txt",
            RelativeTo(_root, path),
            "A module id inside a relative path is a plain segment of the caller's own namespace");

        ModuleStorageNamespace.TryBuild(
            _root, OtherModuleId, "attachments", userId: null, "secret.txt", out var otherPath, out _);
        Assert.AreNotEqual(path, otherPath);
    }

    [TestMethod]
    public void TryBuild_DifferentModulesAndUsers_NeverCollide()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        ModuleStorageNamespace.TryBuild(_root, ModuleId, "c", null, "x", out var moduleGlobal, out _);
        ModuleStorageNamespace.TryBuild(_root, ModuleId, "c", userA.ToString(), "x", out var userAFile, out _);
        ModuleStorageNamespace.TryBuild(_root, ModuleId, "c", userB.ToString(), "x", out var userBFile, out _);
        ModuleStorageNamespace.TryBuild(_root, OtherModuleId, "c", null, "x", out var otherModule, out _);

        var paths = new[] { moduleGlobal, userAFile, userBFile, otherModule };
        CollectionAssert.AllItemsAreUnique(paths);
    }

    [TestMethod]
    public void TryBuild_WhenStorageRootIsMissing_IsRejected()
    {
        var built = ModuleStorageNamespace.TryBuild(
            "  ", ModuleId, "attachments", userId: null, "a.txt", out _, out var error);

        Assert.IsFalse(built);
        Assert.IsNotNull(error);
    }
}

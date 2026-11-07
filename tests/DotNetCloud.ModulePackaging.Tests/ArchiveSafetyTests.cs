using DotNetCloud.ModulePackaging;

namespace DotNetCloud.ModulePackaging.Tests;

/// <summary>
/// Archive-structure hardening tests (F13). These run against synthetic entry descriptors so every
/// hostile shape can be exercised without building a real archive for each one.
/// </summary>
[TestClass]
public class ArchiveSafetyTests
{
    private static PackageEntryInfo File(string name, long length = 100, long compressed = 50, bool symlink = false) =>
        new(name, length, compressed, IsDirectory: false, IsSymlink: symlink);

    private static IReadOnlyList<string> Codes(IEnumerable<PackageEntryInfo> entries) =>
        ArchiveSafetyInspector.Inspect(entries.ToList()).Select(issue => issue.Code).Distinct().ToList();

    [TestMethod]
    public void Inspect_NormalLayout_ReturnsNoIssues()
    {
        var issues = ArchiveSafetyInspector.Inspect(
        [
            File("manifest.json"),
            File("LICENSE"),
            File("README.md"),
            File("CHANGELOG.md"),
            File("files/linux-x64/dotnetcloud.example.dll"),
            File("files/win-x64/dotnetcloud.example.dll"),
        ]);

        Assert.AreEqual(0, issues.Count, string.Join("; ", issues));
    }

    [TestMethod]
    public void Inspect_AbsolutePath_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("/etc/passwd")]).ToList(), PackageIssueCodes.AbsolutePath);
    }

    [TestMethod]
    public void Inspect_DriveLetterPath_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("C:/Windows/system32/x.dll")]).ToList(), PackageIssueCodes.AbsolutePath);
    }

    [TestMethod]
    public void Inspect_ParentTraversal_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("files/../../evil.dll")]).ToList(), PackageIssueCodes.PathTraversal);
    }

    [TestMethod]
    public void Inspect_BackslashSeparator_IsRejected()
    {
        CollectionAssert.Contains(Codes([File(@"files\linux-x64\x.dll")]).ToList(), PackageIssueCodes.InvalidEntryName);
    }

    [TestMethod]
    public void Inspect_EmptySegment_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("files//x.dll")]).ToList(), PackageIssueCodes.InvalidEntryName);
    }

    [TestMethod]
    public void Inspect_ControlCharacter_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("files/evil\u0007.dll")]).ToList(), PackageIssueCodes.InvalidEntryName);
    }

    [TestMethod]
    public void Inspect_TrailingDot_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("files/x.")]).ToList(), PackageIssueCodes.AlternateDataStream);
    }

    [TestMethod]
    public void Inspect_TrailingSpace_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("files/x ")]).ToList(), PackageIssueCodes.AlternateDataStream);
    }

    [TestMethod]
    public void Inspect_AlternateDataStream_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("files/x.dll:evil")]).ToList(), PackageIssueCodes.AlternateDataStream);
    }

    [TestMethod]
    public void Inspect_ReservedDeviceName_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("CON")]).ToList(), PackageIssueCodes.ReservedName);
        CollectionAssert.Contains(Codes([File("files/aux.txt")]).ToList(), PackageIssueCodes.ReservedName);
        CollectionAssert.Contains(Codes([File("files/COM1.dll")]).ToList(), PackageIssueCodes.ReservedName);
    }

    [TestMethod]
    public void Inspect_DuplicateEntry_IsRejected()
    {
        CollectionAssert.Contains(
            Codes([File("files/a.dll"), File("files/a.dll")]).ToList(),
            PackageIssueCodes.DuplicateEntry);
    }

    [TestMethod]
    public void Inspect_CaseCollision_IsRejected()
    {
        CollectionAssert.Contains(
            Codes([File("README.md"), File("readme.md")]).ToList(),
            PackageIssueCodes.CaseCollision);
    }

    [TestMethod]
    public void Inspect_OverlongPath_IsRejected()
    {
        var name = "files/" + new string('a', ModulePackageLimits.MaxPathLength) + ".dll";
        CollectionAssert.Contains(Codes([File(name)]).ToList(), PackageIssueCodes.PathTooLong);
    }

    [TestMethod]
    public void Inspect_TooManySegments_IsRejected()
    {
        var segments = string.Join('/', Enumerable.Repeat("a", ModulePackageLimits.MaxPathSegments + 1));
        CollectionAssert.Contains(Codes([File(segments)]).ToList(), PackageIssueCodes.PathTooLong);
    }

    [TestMethod]
    public void Inspect_SymlinkEntry_IsRejected()
    {
        CollectionAssert.Contains(Codes([File("files/link", symlink: true)]).ToList(), PackageIssueCodes.SymlinkEntry);
    }

    [TestMethod]
    public void Inspect_EntryTooLarge_IsRejected()
    {
        CollectionAssert.Contains(
            Codes([File("files/huge.bin", ModulePackageLimits.MaxEntryBytes + 1)]).ToList(),
            PackageIssueCodes.EntryTooLarge);
    }

    [TestMethod]
    public void Inspect_ZipBombRatio_IsRejected()
    {
        // 5 MiB expanding from 5 KiB is ~1000:1, far above the 200:1 ceiling.
        CollectionAssert.Contains(
            Codes([File("files/bomb.bin", 5 * 1024 * 1024, 5 * 1024)]).ToList(),
            PackageIssueCodes.CompressionRatioExceeded);
    }

    [TestMethod]
    public void Inspect_SmallHighRatioEntry_IsAllowed()
    {
        // Below the ratio floor an entry cannot meaningfully bomb; padding is normal in binaries.
        var codes = Codes([File("files/small.bin", 10_000, 10)]);

        Assert.IsFalse(codes.Contains(PackageIssueCodes.CompressionRatioExceeded));
    }

    [TestMethod]
    public void Inspect_TooManyEntries_IsRejected()
    {
        var entries = Enumerable
            .Range(0, ModulePackageLimits.MaxEntries + 1)
            .Select(index => File($"files/{index}.bin"));

        CollectionAssert.Contains(Codes(entries).ToList(), PackageIssueCodes.EntryCountExceeded);
    }

    [TestMethod]
    public void Inspect_TotalUncompressedTooLarge_IsRejected()
    {
        var entrySize = ModulePackageLimits.MaxEntryBytes;
        var entries = Enumerable
            .Range(0, 5)
            .Select(index => File($"files/{index}.bin", entrySize, entrySize / 10));

        CollectionAssert.Contains(Codes(entries).ToList(), PackageIssueCodes.UncompressedTooLarge);
    }
}

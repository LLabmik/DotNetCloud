using DotNetCloud.CLI.Infrastructure;

namespace DotNetCloud.CLI.Tests.Commands;

/// <summary>
/// Tests for the large-dataset backup engine: part-size parsing, split zip sets, entry sources
/// (single archive, part set, snapshot directory) and the incremental mirror writer. These are the
/// pieces that let a backup exceed a filesystem's single-file limit and stay cheap at 20 TB.
/// </summary>
[TestClass]
public class BackupEngineTests
{
    private readonly List<string> _tempDirectories = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in _tempDirectories)
        {
            try
            { Directory.Delete(directory, recursive: true); }
            catch { /* best effort */ }
        }

        _tempDirectories.Clear();
    }

    [TestMethod]
    public void Parse_TerabyteSize_ReturnsBinaryMultiple()
    {
        Assert.AreEqual(8L * 1024 * 1024 * 1024 * 1024, BackupSizes.Parse("8TB"));
    }

    [TestMethod]
    public void Parse_GigabyteWithDecimal_ReturnsRoundedBytes()
    {
        Assert.AreEqual((long)(1.5 * 1024 * 1024 * 1024), BackupSizes.Parse("1.5GB"));
    }

    [TestMethod]
    public void Parse_PlainNumber_TreatsAsBytes()
    {
        Assert.AreEqual(4096L, BackupSizes.Parse("4096"));
    }

    [TestMethod]
    public void Parse_Null_ReturnsDefault()
    {
        Assert.AreEqual(123L, BackupSizes.Parse(null, 123));
        Assert.AreEqual(0L, BackupSizes.Parse("  "));
    }

    [TestMethod]
    public void Parse_InvalidUnit_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => BackupSizes.Parse("8XB"));
        Assert.ThrowsExactly<FormatException>(() => BackupSizes.Parse("0"));
    }

    [TestMethod]
    public void Complete_SingleArchive_WritesChecksumAndInfoEntry()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "backup.zip");
        var source = WriteFile(directory, "a.bin", 128);

        using var writer = new SplitZipWriter(path, splitSizeBytes: 0, System.IO.Compression.CompressionLevel.Fastest);
        writer.AddFile(source, "data/a.bin", new FileInfo(source).Length);

        var parts = writer.Complete(new BackupInfo { DataFileCount = 1 });

        Assert.HasCount(1, parts);
        Assert.AreEqual(path, parts[0]);
        Assert.IsTrue(File.Exists(path + ".sha256"));
        Assert.IsFalse(File.Exists(path + ".backupset.json"));

        using var source2 = OpenSource(path);
        var info = BackupArchive.DeserializeInfo(ReadEntry(source2, BackupArchive.InfoEntryName));

        Assert.IsNotNull(info);
        Assert.AreEqual("zip", info.Mode);
        Assert.AreEqual(0, info.SplitSizeBytes);
        Assert.AreEqual(2, info.TotalEntryCount, "the data entry plus backup-info.json");
    }

    [TestMethod]
    public void Complete_SplitSet_WritesPartsManifestAndChecksums()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "big.zip");

        var a = WriteFile(directory, "a.bin", 2000);
        var b = WriteFile(directory, "b.bin", 2000);
        var c = WriteFile(directory, "c.bin", 2000);

        using var writer = new SplitZipWriter(path, splitSizeBytes: 4096, System.IO.Compression.CompressionLevel.Fastest);
        writer.AddFile(a, "data/a.bin", 2000);
        writer.AddFile(b, "data/b.bin", 2000);
        writer.AddFile(c, "data/c.bin", 2000);

        var parts = writer.Complete(new BackupInfo());

        Assert.IsGreaterThanOrEqualTo(2, parts.Count);
        Assert.IsNotNull(writer.ManifestPath);
        Assert.IsTrue(File.Exists(writer.ManifestPath));

        foreach (var part in parts)
            Assert.IsTrue(File.Exists(part + ".sha256"), $"missing checksum for {part}");

        using var source = OpenSource(writer.ManifestPath!);
        CollectionAssert.Contains(source.Entries.Select(e => e.FullName).ToList(), "data/a.bin");
        CollectionAssert.Contains(source.Entries.Select(e => e.FullName).ToList(), "data/c.bin");
        CollectionAssert.Contains(source.Entries.Select(e => e.FullName).ToList(), BackupArchive.InfoEntryName);

        var info = BackupArchive.DeserializeInfo(ReadEntry(source, BackupArchive.InfoEntryName));
        Assert.IsNotNull(info);
        Assert.AreEqual(4096, info.SplitSizeBytes);
    }

    [TestMethod]
    public void TryOpen_PartPath_ResolvesSetManifest()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "set.zip");
        var file = WriteFile(directory, "a.bin", 1500);

        using (var writer = new SplitZipWriter(path, splitSizeBytes: 800, System.IO.Compression.CompressionLevel.Fastest))
        {
            writer.AddFile(file, "data/a.bin", 1500);
            writer.Complete(new BackupInfo());
        }

        var firstPart = Directory.GetFiles(directory, "set.part*.zip").OrderBy(p => p).First();

        Assert.IsTrue(BackupSource.TryOpen(firstPart, null, verifyParts: false, out var source, out var error), error);
        using var opened = source!;
        Assert.IsNotNull(opened.GetEntry("data/a.bin"));
    }

    [TestMethod]
    public void TryOpen_CorruptedPartWithVerification_Fails()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "set.zip");
        var file = WriteFile(directory, "a.bin", 1500);

        using (var writer = new SplitZipWriter(path, splitSizeBytes: 800, System.IO.Compression.CompressionLevel.Fastest))
        {
            writer.AddFile(file, "data/a.bin", 1500);
            writer.Complete(new BackupInfo());
        }

        var manifestPath = Path.Combine(directory, "set.backupset.json");
        var part = Directory.GetFiles(directory, "set.part*.zip").OrderBy(p => p).First();

        // Same length, different bytes: only the hash check can catch this.
        var bytes = File.ReadAllBytes(part);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(part, bytes);

        Assert.IsFalse(BackupSource.TryOpen(manifestPath, null, verifyParts: true, out _, out var error));
        StringAssert.Contains(error!, "SHA-256");
    }

    [TestMethod]
    public void TryOpen_MissingPart_ReportsMissingPart()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "set.zip");
        var file = WriteFile(directory, "a.bin", 1500);

        using (var writer = new SplitZipWriter(path, splitSizeBytes: 800, System.IO.Compression.CompressionLevel.Fastest))
        {
            writer.AddFile(file, "data/a.bin", 1500);
            writer.Complete(new BackupInfo());
        }

        var lastPart = Directory.GetFiles(directory, "set.part*.zip").OrderBy(p => p).Last();
        File.Delete(lastPart);

        Assert.IsFalse(BackupSource.TryOpen(Path.Combine(directory, "set.backupset.json"), null, false, out _, out var error));
        StringAssert.Contains(error!, "missing");
    }

    [TestMethod]
    public void TryOpen_SnapshotRoot_PicksNewestCompleteSnapshot()
    {
        var root = CreateTempDirectory();
        WriteSnapshot(root, "20260101-000000", "old.txt", "old");
        WriteSnapshot(root, "20260102-000000", "new.txt", "new");
        Directory.CreateDirectory(Path.Combine(root, "20260103-000000.partial"));

        Assert.IsTrue(BackupSource.TryOpen(root, null, false, out var source, out var error), error);
        using var opened = source!;

        Assert.IsNotNull(opened.GetEntry("new.txt"));
        Assert.IsNull(opened.GetEntry("old.txt"));
        Assert.AreEqual(Path.Combine(root, "20260102-000000"), opened.DisplayName);
    }

    [TestMethod]
    public void TryOpen_SnapshotRootWithExplicitName_OpensThatSnapshot()
    {
        var root = CreateTempDirectory();
        WriteSnapshot(root, "20260101-000000", "old.txt", "old");
        WriteSnapshot(root, "20260102-000000", "new.txt", "new");

        Assert.IsTrue(BackupSource.TryOpen(root, "20260101-000000", false, out var source, out var error), error);
        using var opened = source!;

        Assert.IsNotNull(opened.GetEntry("old.txt"));
    }

    [TestMethod]
    public void Dispose_WithoutComplete_DeletesPartialParts()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "aborted.zip");
        var file = WriteFile(directory, "a.bin", 1500);

        using (var writer = new SplitZipWriter(path, splitSizeBytes: 800, System.IO.Compression.CompressionLevel.Fastest))
        {
            writer.AddFile(file, "data/a.bin", 1500);
        }

        Assert.IsEmpty(Directory.GetFiles(directory, "aborted.part*.zip"));
        Assert.IsFalse(File.Exists(Path.Combine(directory, "aborted.backupset.json")));
    }

    [TestMethod]
    public void IsContentAddressed_StorageBlobs_ReturnsTrue()
    {
        var hash = new string('a', 64);

        Assert.IsTrue(MirrorSnapshotWriter.IsContentAddressed($"storage/files/aa/bb/{hash}"));
        Assert.IsTrue(MirrorSnapshotWriter.IsContentAddressed($"storage\\chunks\\aa\\bb\\{hash}"));
        Assert.IsFalse(MirrorSnapshotWriter.IsContentAddressed($"storage/files/aa/bb/{hash}.tmp"));
        Assert.IsFalse(MirrorSnapshotWriter.IsContentAddressed($"storage/files/aa/bb/shortname"));
        Assert.IsFalse(MirrorSnapshotWriter.IsContentAddressed($"oidc-keys/{hash}"));
    }

    [TestMethod]
    public async Task RunAsync_SecondSnapshot_HardlinksUnchangedBlobs()
    {
        var directory = CreateTempDirectory();
        var dataDirectory = Path.Combine(directory, "data");
        var configDirectory = Path.Combine(directory, "config");
        var target = Path.Combine(directory, "mirror");

        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "config.json"), "{}");

        var blobHash = new string('b', 64);
        var blobDirectory = Path.Combine(dataDirectory, "storage", "files", "bb", "bb");
        Directory.CreateDirectory(blobDirectory);
        var blob = Path.Combine(blobDirectory, blobHash);
        File.WriteAllText(blob, "blob-content");

        var newBlobHash = new string('c', 64);
        var newBlob = Path.Combine(blobDirectory, newBlobHash);

        var linker = new RecordingHardLinker();
        var writer = new MirrorSnapshotWriter(
            target, dataDirectory, configDirectory, directory, "tester",
            excludePaths: [target], hardLinker: linker);

        var first = await writer.RunAsync(WriteDumpAsync, CancellationToken.None);

        Assert.AreEqual(1, first.Files);
        Assert.AreEqual(0, first.HardLinkedFiles);
        Assert.AreEqual(1, first.CopiedFiles);
        Assert.IsTrue(File.Exists(Path.Combine(first.SnapshotPath, BackupArchive.InfoEntryName)));
        Assert.IsTrue(File.Exists(Path.Combine(first.SnapshotPath, "data", "storage", "files", "bb", "bb", blobHash)));
        Assert.IsEmpty(Directory.GetDirectories(target, "*.partial"));

        File.WriteAllText(newBlob, "second-blob");

        var second = await writer.RunAsync(WriteDumpAsync, CancellationToken.None);

        Assert.AreEqual(2, second.Files);
        Assert.AreEqual(1, second.HardLinkedFiles);
        Assert.AreEqual(1, second.CopiedFiles);
        Assert.AreEqual(Path.GetFileName(first.SnapshotPath), second.PreviousSnapshot);
        Assert.HasCount(1, linker.Created);
        StringAssert.Contains(linker.Created[0], blobHash);

        var info = BackupArchive.DeserializeInfo(
            File.ReadAllText(Path.Combine(second.SnapshotPath, BackupArchive.InfoEntryName)));
        Assert.IsNotNull(info);
        Assert.AreEqual("mirror", info.Mode);
        Assert.AreEqual(1, info.HardLinkedFiles);
        Assert.AreEqual(first.SnapshotPath.Split(Path.DirectorySeparatorChar)[^1], info.PreviousSnapshot);
    }

    [TestMethod]
    public async Task RunAsync_PreviousPartialSnapshot_IsResumed()
    {
        var directory = CreateTempDirectory();
        var dataDirectory = Path.Combine(directory, "data");
        var configDirectory = Path.Combine(directory, "config");
        var target = Path.Combine(directory, "mirror");

        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(Path.Combine(configDirectory, "config.json"), "{}");

        var blobHash = new string('d', 64);
        var blobDirectory = Path.Combine(dataDirectory, "storage", "files", "dd", "dd");
        Directory.CreateDirectory(blobDirectory);
        var blob = Path.Combine(blobDirectory, blobHash);
        File.WriteAllText(blob, "resume-me");

        var partial = Path.Combine(target, "20260101-000000.partial");
        var partialBlob = Path.Combine(partial, "data", "storage", "files", "dd", "dd", blobHash);
        Directory.CreateDirectory(Path.GetDirectoryName(partialBlob)!);
        File.WriteAllText(partialBlob, "resume-me");

        var writer = new MirrorSnapshotWriter(
            target, dataDirectory, configDirectory, directory, "tester",
            excludePaths: [target], hardLinker: new RecordingHardLinker());

        var result = await writer.RunAsync(WriteDumpAsync, CancellationToken.None);

        Assert.AreEqual(Path.Combine(target, "20260101-000000"), result.SnapshotPath);
        Assert.AreEqual(0, result.CopiedFiles, "the blob already present in the partial snapshot must not be recopied");
        Assert.AreEqual("resume-me",
            File.ReadAllText(Path.Combine(result.SnapshotPath, "data", "storage", "files", "dd", "dd", blobHash)));
    }

    private static Task<bool> WriteDumpAsync(string path)
    {
        File.WriteAllText(path, "-- dump");
        return Task.FromResult(true);
    }

    private string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "dnc-backup-engine-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }

    private static string WriteFile(string directory, string name, int size)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    private static void WriteSnapshot(string root, string name, string fileName, string content)
    {
        var snapshot = Path.Combine(root, name);
        Directory.CreateDirectory(snapshot);
        File.WriteAllText(Path.Combine(snapshot, fileName), content);
        File.WriteAllText(
            Path.Combine(snapshot, BackupArchive.InfoEntryName),
            BackupArchive.SerializeInfo(new BackupInfo { Mode = "mirror" }));
    }

    private static BackupSource OpenSource(string path)
    {
        Assert.IsTrue(BackupSource.TryOpen(path, null, verifyParts: false, out var source, out var error), error);
        return source!;
    }

    private static string ReadEntry(BackupSource source, string name)
    {
        var entry = source.GetEntry(name);
        Assert.IsNotNull(entry, $"missing entry '{name}'");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Records hardlink attempts and creates a real copy so the snapshot tree is complete.</summary>
    private sealed class RecordingHardLinker : IHardLinker
    {
        public List<string> Created { get; } = [];

        public bool TryCreate(string existingPath, string newPath)
        {
            Created.Add(newPath);
            File.Copy(existingPath, newPath, overwrite: true);
            return true;
        }
    }
}

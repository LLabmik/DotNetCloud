using System.Text;
using DotNetCloud.Modules.Email.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Email.Tests.Services;

/// <summary>
/// Tests for the attachment store's storage-key handling.
/// </summary>
/// <remarks>
/// The key is supplied by the client on the attachment and send endpoints. Before this was
/// validated, the key was used directly as a filesystem path, so any authenticated user could read
/// arbitrary files as the email module process — including the shared Data Protection key ring and
/// other users' attachment content.
/// </remarks>
[TestClass]
public sealed class FileSystemAttachmentStorageTests
{
    private string _basePath = null!;
    private FileSystemAttachmentStorage _storage = null!;

    [TestInitialize]
    public void Setup()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"dnc-email-att-{Guid.CreateVersion7():N}");
        Directory.CreateDirectory(_basePath);

        _storage = new FileSystemAttachmentStorage(
            Options.Create(new AttachmentStorageOptions { BasePath = _basePath }),
            NullLogger<FileSystemAttachmentStorage>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("../../appsettings.json")]
    [DataRow("../../../../../etc/passwd")]
    [DataRow("/etc/passwd")]
    [DataRow("C:\\Windows\\win.ini")]
    [DataRow("..\\..\\evil.txt")]
    [DataRow("contains/slash")]
    [DataRow("ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [DataRow("tooshort")]
    [DataRow("")]
    public async Task OpenReadAsync_KeyThatIsNotAContentHash_ReturnsNull(string storageKey)
    {
        Assert.IsNull(await _storage.OpenReadAsync(storageKey));
    }

    [TestMethod]
    [DataRow("../../appsettings.json")]
    [DataRow("/etc/passwd")]
    [DataRow("not-a-hash")]
    public async Task GetSizeAsync_KeyThatIsNotAContentHash_ReturnsZero(string storageKey)
    {
        Assert.AreEqual(0L, await _storage.GetSizeAsync(storageKey));
    }

    [TestMethod]
    [DataRow("../../appsettings.json")]
    [DataRow("/etc/passwd")]
    public async Task DeleteAsync_KeyThatIsNotAContentHash_ReturnsFalse(string storageKey)
    {
        Assert.IsFalse(await _storage.DeleteAsync(storageKey));
    }

    [TestMethod]
    [DataRow("../../appsettings.json")]
    [DataRow("/etc/passwd")]
    public void GetCreationTime_KeyThatIsNotAContentHash_ReturnsNull(string storageKey)
    {
        Assert.IsNull(_storage.GetCreationTime(storageKey));
    }

    [TestMethod]
    public async Task StoreAsync_ThenOpenReadAsync_RoundTripsThroughAValidatedKey()
    {
        var payload = Encoding.UTF8.GetBytes("attachment bytes");

        using var source = new MemoryStream(payload);
        var stored = await _storage.StoreAsync(source, "note.txt", "text/plain");

        Assert.AreEqual(64, stored.StorageKey.Length);

        await using var read = await _storage.OpenReadAsync(stored.StorageKey);
        Assert.IsNotNull(read);

        using var buffered = new MemoryStream();
        await read!.CopyToAsync(buffered);
        CollectionAssert.AreEqual(payload, buffered.ToArray());
    }
}

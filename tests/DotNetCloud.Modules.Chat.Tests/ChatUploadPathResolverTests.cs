using DotNetCloud.Modules.Chat.Data.Services;
using Microsoft.Extensions.Configuration;

namespace DotNetCloud.Modules.Chat.Tests;

/// <summary>
/// Tests for <see cref="ChatUploadPathResolver"/>.
/// </summary>
[TestClass]
public class ChatUploadPathResolverTests
{
    private static IConfiguration Config(string? storageRoot)
    {
        var values = new Dictionary<string, string?>();
        if (storageRoot is not null)
        {
            values["Files:Storage:RootPath"] = storageRoot;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [TestMethod]
    public void Resolve_ConfiguredStorageRoot_ReturnsConfiguredPath()
    {
        var result = ChatUploadPathResolver.Resolve(Config("/srv/storage-root"), "/var/lib/dotnetcloud");

        Assert.AreEqual("/srv/storage-root", result);
    }

    [TestMethod]
    public void Resolve_NoConfiguredRoot_UsesDataDirectoryStorageSubdirectory()
    {
        var result = ChatUploadPathResolver.Resolve(Config(null), "/var/lib/dotnetcloud");

        Assert.AreEqual(Path.Combine("/var/lib/dotnetcloud", "storage"), result);
    }

    [TestMethod]
    public void Resolve_BlankConfiguredRoot_UsesDataDirectoryStorageSubdirectory()
    {
        var result = ChatUploadPathResolver.Resolve(Config("   "), "/var/lib/dotnetcloud");

        Assert.AreEqual(Path.Combine("/var/lib/dotnetcloud", "storage"), result);
    }

    [TestMethod]
    public void Resolve_NoConfiguredRootOrDataDirectory_FallsBackToCurrentDirectoryStorage()
    {
        var result = ChatUploadPathResolver.Resolve(Config(null), null);

        Assert.AreEqual(Path.Combine(Directory.GetCurrentDirectory(), "storage"), result);
    }

    [TestMethod]
    public void Resolve_BlankDataDirectory_FallsBackToCurrentDirectoryStorage()
    {
        var result = ChatUploadPathResolver.Resolve(Config(null), "  ");

        Assert.AreEqual(Path.Combine(Directory.GetCurrentDirectory(), "storage"), result);
    }
}

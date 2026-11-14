namespace DotNetCloud.Core.Tests.Storage;

using DotNetCloud.Core.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Unit tests for <see cref="MediaCachePathResolver"/> — media assets must resolve to the same
/// persistent directory from the core process and from a module host.
/// </summary>
[TestClass]
public class MediaCachePathResolverTests
{
    [TestMethod]
    public void ResolveMediaCachePath_ExplicitMediaCachePath_Wins()
    {
        var config = BuildConfig(("Files:Storage:MediaCachePath", "/srv/custom-cache"),
            ("Files:Storage:RootPath", "/srv/storage"));

        var result = MediaCachePathResolver.ResolveMediaCachePath(config, dataDirectory: "/var/lib/dotnetcloud");

        Assert.AreEqual("/srv/custom-cache", result);
    }

    [TestMethod]
    public void ResolveMediaCachePath_StorageRootConfigured_UsesMediaCacheSubdirectory()
    {
        var config = BuildConfig(("Files:Storage:RootPath", "/srv/storage"));

        var result = MediaCachePathResolver.ResolveMediaCachePath(config, dataDirectory: "/var/lib/dotnetcloud");

        Assert.AreEqual(Path.Combine("/srv/storage", ".media-cache"), result);
    }

    [TestMethod]
    public void ResolveMediaCachePath_OnlyDataDirectoryConfigured_UsesPersistentStorage()
    {
        var config = BuildConfig();

        var result = MediaCachePathResolver.ResolveMediaCachePath(config, dataDirectory: "/var/lib/dotnetcloud");

        var expected = Path.Combine("/var/lib/dotnetcloud", "storage", ".media-cache");
        Assert.AreEqual(expected, result, "The cache must be persistent when DOTNETCLOUD_DATA_DIR is available.");
    }

    [TestMethod]
    public void ResolveMediaCachePath_NothingConfigured_FallsBackToTempDirectory()
    {
        var config = BuildConfig();

        var result = MediaCachePathResolver.ResolveMediaCachePath(config, dataDirectory: null);

        Assert.IsTrue(result.EndsWith(MediaCachePathResolver.MediaCacheFolderName, StringComparison.Ordinal),
            $"Expected a '{MediaCachePathResolver.MediaCacheFolderName}' leaf directory, got '{result}'.");
    }

    [TestMethod]
    public void ResolveMediaCachePath_CoreAndHostConfigurations_Agree()
    {
        // Core.Server propagates Files:Storage:RootPath; a module host resolves the same root from
        // DOTNETCLOUD_DATA_DIR. Both must end up at the identical media-cache directory.
        var coreConfig = BuildConfig(("Files:Storage:RootPath", "/var/lib/dotnetcloud/storage"));
        var hostConfig = BuildConfig();

        var corePath = MediaCachePathResolver.ResolveMediaCachePath(coreConfig, dataDirectory: null);
        var hostPath = MediaCachePathResolver.ResolveMediaCachePath(hostConfig, dataDirectory: "/var/lib/dotnetcloud");

        Assert.AreEqual(corePath, hostPath);
    }

    [TestMethod]
    public void ResolveStorageRoot_StorageRootConfigured_ReturnsConfiguredRoot()
    {
        var config = BuildConfig(("Files:Storage:RootPath", "/srv/storage"));

        Assert.AreEqual("/srv/storage", MediaCachePathResolver.ResolveStorageRoot(config, "/var/lib/dotnetcloud"));
    }

    [TestMethod]
    public void ResolveStorageRoot_OnlyDataDirectoryConfigured_ReturnsDataStorage()
    {
        var config = BuildConfig();

        var result = MediaCachePathResolver.ResolveStorageRoot(config, "/var/lib/dotnetcloud");

        Assert.AreEqual(Path.Combine("/var/lib/dotnetcloud", "storage"), result);
    }

    private static IConfiguration BuildConfig(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();
}

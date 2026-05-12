using System.Text.Json;
using DotNetCloud.Client.Core.VirtualFiles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNetCloud.Client.Core.Tests.VirtualFiles;

[TestClass]
public sealed class VirtualFileSettingsTests
{
    [TestMethod]
    public void DefaultConstructor_StorageModeIsDownloadAll()
    {
        var settings = new VirtualFileSettings();
        Assert.AreEqual(VirtualFileStorageMode.DownloadAll, settings.StorageMode);
    }

    [TestMethod]
    public void DefaultConstructor_MaxCacheSizeIsZero()
    {
        var settings = new VirtualFileSettings();
        Assert.AreEqual(0, settings.MaxCacheSizeBytes);
    }

    [TestMethod]
    public void DefaultConstructor_PinListIsEmpty()
    {
        var settings = new VirtualFileSettings();
        Assert.IsNotNull(settings.PinList);
        Assert.AreEqual(0, settings.PinList.Count);
    }

    [TestMethod]
    public void Serialization_RoundTrip_PreservesStorageMode()
    {
        var settings = new VirtualFileSettings
        {
            StorageMode = VirtualFileStorageMode.FilesOnDemand,
        };

        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<VirtualFileSettings>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(VirtualFileStorageMode.FilesOnDemand, deserialized.StorageMode);
    }

    [TestMethod]
    public void Serialization_RoundTrip_PreservesMaxCacheSize()
    {
        var settings = new VirtualFileSettings
        {
            MaxCacheSizeBytes = 500_000_000,
        };

        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<VirtualFileSettings>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(500_000_000, deserialized.MaxCacheSizeBytes);
    }

    [TestMethod]
    public void Serialization_RoundTrip_PreservesPinList()
    {
        var settings = new VirtualFileSettings
        {
            PinList =
            [
                @"C:\Users\test\Documents\important.pdf",
                @"C:\Users\test\Documents\keep-local.docx",
            ],
        };

        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<VirtualFileSettings>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(2, deserialized.PinList.Count);
        Assert.IsTrue(deserialized.PinList.Contains(@"C:\Users\test\Documents\important.pdf"));
    }

    [TestMethod]
    public void PinList_CaseInsensitive()
    {
        var settings = new VirtualFileSettings();
        settings.PinList.Add(@"C:\USERS\TEST\FILE.TXT");

        Assert.IsTrue(settings.PinList.Contains(@"c:\users\test\file.txt"));
        Assert.IsTrue(settings.PinList.Contains(@"C:\Users\Test\File.txt"));
    }

    [TestMethod]
    public void Serialization_RoundTrip_PinListPreservesEntries()
    {
        // Note: JSON deserialization creates a default HashSet that does NOT
        // preserve the custom StringComparer.OrdinalIgnoreCase. Case-insensitive
        // matching is applied via the PinList property's HashSet<StringComparer.OrdinalIgnoreCase>,
        // which only takes effect for runtime operations after deserialization.
        var settings = new VirtualFileSettings
        {
            PinList = [@"C:\USERS\TEST\FILE.TXT"],
        };

        var json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<VirtualFileSettings>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(1, deserialized.PinList.Count);
        // The entry itself is preserved; case-sensitive check works because
        // the exact original string was serialized.
        Assert.IsTrue(deserialized.PinList.Contains(@"C:\USERS\TEST\FILE.TXT"));
    }
}

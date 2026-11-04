using System.IO.Compression;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Tests.DTOs;

/// <summary>
/// Tests for the backup compression-name mapping used by the CLI and the server.
/// </summary>
[TestClass]
public class BackupCompressionTests
{
    [TestMethod]
    public void Parse_Store_ReturnsNoCompression()
    {
        Assert.AreEqual(CompressionLevel.NoCompression, BackupCompression.Parse("store"));
    }

    [TestMethod]
    public void Parse_NoneAlias_ReturnsNoCompression()
    {
        Assert.AreEqual(CompressionLevel.NoCompression, BackupCompression.Parse("none"));
    }

    [TestMethod]
    public void Parse_Fastest_ReturnsFastest()
    {
        Assert.AreEqual(CompressionLevel.Fastest, BackupCompression.Parse("fastest"));
    }

    [TestMethod]
    public void Parse_Optimal_ReturnsOptimal()
    {
        Assert.AreEqual(CompressionLevel.Optimal, BackupCompression.Parse("optimal"));
    }

    [TestMethod]
    public void Parse_IsCaseInsensitive()
    {
        Assert.AreEqual(CompressionLevel.NoCompression, BackupCompression.Parse("STORE"));
        Assert.AreEqual(CompressionLevel.Fastest, BackupCompression.Parse("Fastest"));
    }

    [TestMethod]
    public void Parse_NullEmptyOrUnknown_FallsBackToOptimal()
    {
        Assert.AreEqual(CompressionLevel.Optimal, BackupCompression.Parse(null));
        Assert.AreEqual(CompressionLevel.Optimal, BackupCompression.Parse(""));
        Assert.AreEqual(CompressionLevel.Optimal, BackupCompression.Parse("bogus"));
    }

    [TestMethod]
    public void Default_IsOptimal()
    {
        Assert.AreEqual("optimal", BackupCompression.Default);
    }
}

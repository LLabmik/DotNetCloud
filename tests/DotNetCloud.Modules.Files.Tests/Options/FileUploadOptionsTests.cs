using DotNetCloud.Modules.Files.Options;

namespace DotNetCloud.Modules.Files.Tests.Options;

/// <summary>
/// Tests for <see cref="FileUploadOptions"/> upload size defaults.
/// </summary>
[TestClass]
public class FileUploadOptionsTests
{
    [TestMethod]
    public void MaxFileSizeBytes_Default_SupportsAtLeast16GiB()
    {
        // Regression for large-file uploads: the default cap must comfortably exceed a
        // 16 GiB file. It was previously 15 GiB (16,106,127,360) — below 16 GiB — which
        // silently rejected files that size at upload initiation.
        var options = new FileUploadOptions();

        const long sixteenGiB = 16L * 1024L * 1024L * 1024L;
        Assert.IsTrue(
            options.MaxFileSizeBytes >= sixteenGiB,
            $"Default MaxFileSizeBytes ({options.MaxFileSizeBytes}) must be >= 16 GiB ({sixteenGiB}).");
    }
}

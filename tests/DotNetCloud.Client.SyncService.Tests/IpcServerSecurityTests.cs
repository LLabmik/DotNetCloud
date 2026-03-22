using DotNetCloud.Client.SyncService.Ipc;

namespace DotNetCloud.Client.SyncService.Tests;

[TestClass]
public sealed class IpcServerSecurityTests
{
    [TestMethod]
    public void RestrictUnixSocketPermissions_SetsSocketModeTo600OnLinux()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-ipc-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var socketPath = Path.Combine(tempDir, "sync.sock");

        try
        {
            // Create a file to simulate the bound socket path.
            File.WriteAllText(socketPath, string.Empty);

            IpcServer.RestrictUnixSocketPermissions(socketPath);

            var mode = File.GetUnixFileMode(socketPath);
            var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            Assert.AreEqual(expected, mode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

using DotNetCloud.Modules.Files.UI;

namespace DotNetCloud.Modules.Files.Tests.UI;

[TestClass]
public sealed class FileUploadComponentTests
{
    // ── Utility method tests ────────────────────────────────────────────

    [TestMethod]
    [DataRow(0L, "0 B")]
    [DataRow(512L, "512 B")]
    [DataRow(1023L, "1023 B")]
    [DataRow(1024L, "1.0 KB")]
    [DataRow(10240L, "10.0 KB")]
    [DataRow(1048576L, "1.0 MB")]
    [DataRow(1073741824L, "1.00 GB")]
    [DataRow(5368709120L, "5.00 GB")]
    public void FormatSize_ReturnsExpectedString(long bytes, string expected)
    {
        var result = TestableFileUploadComponent.InvokeFormatSize(bytes);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("short.txt", 32, "short.txt")]
    [DataRow("exactly-thirty-two-chars-ab.txt", 32, "exactly-thirty-two-chars-ab.txt")]
    [DataRow("this-is-a-very-long-filename-that-exceeds-limit.txt", 32, "this-is-a-very-long-filenam….txt")]
    public void TruncateName_ReturnsExpectedString(string name, int maxLength, string expected)
    {
        var result = TestableFileUploadComponent.InvokeTruncateName(name, maxLength);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(UploadStatus.Complete, "progress-bar-fill--success")]
    [DataRow(UploadStatus.Failed, "progress-bar-fill--error")]
    [DataRow(UploadStatus.Uploading, "")]
    [DataRow(UploadStatus.Pending, "")]
    public void GetProgressClass_ReturnsExpectedCssClass(UploadStatus status, string expected)
    {
        var result = TestableFileUploadComponent.InvokeGetProgressClass(status);
        Assert.AreEqual(expected, result);
    }

    // ── JS callback tests ───────────────────────────────────────────────

    [TestMethod]
    public void ApplyProgress_UpdatesFileProgressAndStatus()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        component.AddTestFile("test.txt", 1024, "text/plain");

        // Act
        component.ApplyProgress(0, 42, "Uploading...");

        // Assert
        Assert.AreEqual(42, component.QueuedFiles[0].Progress);
        Assert.AreEqual("Uploading...", component.QueuedFiles[0].StatusText);
    }

    [TestMethod]
    public void ApplyProgress_InvalidIndex_DoesNotThrow()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        component.AddTestFile("test.txt", 1024, "text/plain");

        // Act — should not throw
        component.ApplyProgress(-1, 50, "test");
        component.ApplyProgress(999, 50, "test");

        // Assert — original file unchanged
        Assert.AreEqual(0, component.QueuedFiles[0].Progress);
    }

    [TestMethod]
    public void ApplyComplete_SetsStatusToComplete()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        component.AddTestFile("test.txt", 1024, "text/plain");
        component.QueuedFiles[0].Status = UploadStatus.Uploading;

        // Act
        component.ApplyComplete(0);

        // Assert
        Assert.AreEqual(UploadStatus.Complete, component.QueuedFiles[0].Status);
        Assert.AreEqual(100, component.QueuedFiles[0].Progress);
        Assert.AreEqual("Complete", component.QueuedFiles[0].StatusText);
    }

    [TestMethod]
    public void ApplyError_SetsStatusToFailed()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        component.AddTestFile("test.txt", 1024, "text/plain");
        component.QueuedFiles[0].Status = UploadStatus.Uploading;

        // Act
        component.ApplyError(0, "Network timeout");

        // Assert
        Assert.AreEqual(UploadStatus.Failed, component.QueuedFiles[0].Status);
        Assert.AreEqual("Failed", component.QueuedFiles[0].StatusText);
    }

    [TestMethod]
    public void ApplyComplete_InvalidIndex_DoesNotThrow()
    {
        var component = new TestableFileUploadComponent();

        // Act — should not throw on empty list
        component.ApplyComplete(0);
        component.ApplyComplete(-1);
    }

    [TestMethod]
    public void ApplyError_InvalidIndex_DoesNotThrow()
    {
        var component = new TestableFileUploadComponent();

        // Act — should not throw on empty list
        component.ApplyError(0, "error");
        component.ApplyError(-1, "error");
    }

    // ── Queue management tests ──────────────────────────────────────────

    [TestMethod]
    public void AddTestFile_AddsToQueue()
    {
        var component = new TestableFileUploadComponent();

        component.AddTestFile("a.txt", 100, "text/plain");
        component.AddTestFile("b.pdf", 200, "application/pdf");

        Assert.AreEqual(2, component.QueuedFiles.Count);
        Assert.AreEqual("a.txt", component.QueuedFiles[0].Name);
        Assert.AreEqual("b.pdf", component.QueuedFiles[1].Name);
        Assert.AreEqual(100, component.QueuedFiles[0].Size);
        Assert.AreEqual(200, component.QueuedFiles[1].Size);
    }

    [TestMethod]
    public void MultipleFiles_ProgressTrackedIndependently()
    {
        var component = new TestableFileUploadComponent();
        component.AddTestFile("a.txt", 100, "text/plain");
        component.AddTestFile("b.txt", 200, "text/plain");

        component.ApplyProgress(0, 50, "Uploading...");
        component.ApplyComplete(1);

        Assert.AreEqual(50, component.QueuedFiles[0].Progress);
        Assert.AreEqual(UploadStatus.Pending, component.QueuedFiles[0].Status);
        Assert.AreEqual(100, component.QueuedFiles[1].Progress);
        Assert.AreEqual(UploadStatus.Complete, component.QueuedFiles[1].Status);
    }

    /// <summary>
    /// Test subclass that exposes protected static methods and internal state
    /// for unit testing without requiring JS interop or Blazor rendering.
    /// </summary>
    private sealed class TestableFileUploadComponent : FileUploadComponent
    {
        public IReadOnlyList<UploadFileItem> QueuedFiles => Files;

        public void AddTestFile(string name, long size, string contentType)
        {
            var field = typeof(FileUploadComponent)
                .GetField("_files", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_files field not found.");

            var files = (List<UploadFileItem>)field.GetValue(this)!;
            files.Add(new UploadFileItem
            {
                Name = name,
                Size = size,
                ContentType = contentType
            });
        }

        public static string InvokeFormatSize(long bytes) => FormatSize(bytes);
        public static string InvokeTruncateName(string name, int maxLength = 32) => TruncateName(name, maxLength);
        public static string InvokeGetProgressClass(UploadStatus status) => GetProgressClass(status);
    }
}

using System.Reflection;
using DotNetCloud.Modules.Files.UI;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNetCloud.Modules.Files.Tests.UI;

[TestClass]
public sealed class FileUploadComponentTests
{
    [TestMethod]
    public async Task HandleFileSelected_NotUploading_AddsAllSelectedFiles()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        var args = CreateInputChangeArgs(
            new FakeBrowserFile("one.txt", 128, "text/plain"),
            new FakeBrowserFile("two.txt", 256, "text/plain"));

        // Act
        await component.InvokeHandleFileSelected(args);

        // Assert
        Assert.AreEqual(2, component.QueuedFiles.Count);
        Assert.AreEqual("one.txt", component.QueuedFiles[0].Name);
        Assert.AreEqual("two.txt", component.QueuedFiles[1].Name);
        CollectionAssert.AreEqual(CreateSequence(128), component.QueuedFiles[0].BufferedContent);
        CollectionAssert.AreEqual(CreateSequence(256), component.QueuedFiles[1].BufferedContent);
    }

    [TestMethod]
    public async Task HandleFileSelected_WhenUploading_IgnoresNewSelection()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        await component.InvokeHandleFileSelected(CreateInputChangeArgs(new FakeBrowserFile("existing.txt", 64, "text/plain")));
        component.SetUploadingStateForTest(true);

        var args = CreateInputChangeArgs(
            new FakeBrowserFile("new-a.txt", 32, "text/plain"),
            new FakeBrowserFile("new-b.txt", 16, "text/plain"));

        // Act
        await component.InvokeHandleFileSelected(args);

        // Assert
        Assert.AreEqual(1, component.QueuedFiles.Count);
        Assert.AreEqual("existing.txt", component.QueuedFiles[0].Name);
    }

    [TestMethod]
    public async Task HandleFileSelected_BuffersContentFromEachSelectedFile()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        var args = CreateInputChangeArgs(
            new FakeBrowserFile("first.bin", 3, "application/octet-stream"),
            new FakeBrowserFile("second.bin", 5, "application/octet-stream"));

        // Act
        await component.InvokeHandleFileSelected(args);

        // Assert
        Assert.AreEqual(2, component.QueuedFiles.Count);
        CollectionAssert.AreEqual(CreateSequence(3), component.QueuedFiles[0].BufferedContent);
        CollectionAssert.AreEqual(CreateSequence(5), component.QueuedFiles[1].BufferedContent);
    }

    [TestMethod]
    public void MapUploadErrorMessage_ReaderCompletedMessage_ReturnsFriendlyText()
    {
        // Arrange
        const string raw = "Reading is not allowed after reader was completed.";

        // Act
        var mapped = InvokeMapUploadErrorMessage(raw);

        // Assert
        Assert.AreEqual("One or more selected files are no longer available to read. Please reselect the files and try again.", mapped);
    }

    [TestMethod]
    public void MapUploadErrorMessage_NullOrWhitespace_ReturnsGenericMessage()
    {
        // Act
        var mapped = InvokeMapUploadErrorMessage(" ");

        // Assert
        Assert.AreEqual("Upload failed unexpectedly. Please try again.", mapped);
    }

    private static InputFileChangeEventArgs CreateInputChangeArgs(params IBrowserFile[] files) =>
        new(files);

    private sealed class TestableFileUploadComponent : FileUploadComponent
    {
        public IReadOnlyList<UploadFileItem> QueuedFiles => Files;

        public Task InvokeHandleFileSelected(InputFileChangeEventArgs args) => HandleFileSelected(args);

        public void SetUploadingStateForTest(bool isUploading)
        {
            var field = typeof(FileUploadComponent).GetField("_isUploading", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_isUploading field not found.");

            field.SetValue(this, isUploading);
        }
    }

    private sealed class FakeBrowserFile(string name, long size, string contentType) : IBrowserFile
    {
        private readonly byte[] _content = CreateSequence((int)size);

        public string Name { get; } = name;

        public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;

        public long Size { get; } = size;

        public string ContentType { get; } = contentType;

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize)
            {
                throw new IOException("File exceeds max allowed size.");
            }

            return new MemoryStream(_content, writable: false);
        }
    }

    private static byte[] CreateSequence(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return data;
    }

    private static string InvokeMapUploadErrorMessage(string? message)
    {
        var method = typeof(FileUploadComponent).GetMethod("MapUploadErrorMessage", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MapUploadErrorMessage method not found.");

        return (string)(method.Invoke(null, [message])
            ?? throw new InvalidOperationException("MapUploadErrorMessage returned null."));
    }
}

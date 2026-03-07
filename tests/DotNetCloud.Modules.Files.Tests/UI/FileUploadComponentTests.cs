using System.Reflection;
using DotNetCloud.Modules.Files.UI;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNetCloud.Modules.Files.Tests.UI;

[TestClass]
public sealed class FileUploadComponentTests
{
    [TestMethod]
    public void HandleFileSelected_NotUploading_AddsAllSelectedFiles()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        var args = CreateInputChangeArgs(
            new FakeBrowserFile("one.txt", 128, "text/plain"),
            new FakeBrowserFile("two.txt", 256, "text/plain"));

        // Act
        component.InvokeHandleFileSelected(args);

        // Assert
        Assert.AreEqual(2, component.QueuedFiles.Count);
        Assert.AreEqual("one.txt", component.QueuedFiles[0].Name);
        Assert.AreEqual("two.txt", component.QueuedFiles[1].Name);
    }

    [TestMethod]
    public void HandleFileSelected_WhenUploading_IgnoresNewSelection()
    {
        // Arrange
        var component = new TestableFileUploadComponent();
        component.InvokeHandleFileSelected(CreateInputChangeArgs(new FakeBrowserFile("existing.txt", 64, "text/plain")));
        component.SetUploadingStateForTest(true);

        var args = CreateInputChangeArgs(
            new FakeBrowserFile("new-a.txt", 32, "text/plain"),
            new FakeBrowserFile("new-b.txt", 16, "text/plain"));

        // Act
        component.InvokeHandleFileSelected(args);

        // Assert
        Assert.AreEqual(1, component.QueuedFiles.Count);
        Assert.AreEqual("existing.txt", component.QueuedFiles[0].Name);
    }

    private static InputFileChangeEventArgs CreateInputChangeArgs(params IBrowserFile[] files) =>
        new(files);

    private sealed class TestableFileUploadComponent : FileUploadComponent
    {
        public IReadOnlyList<UploadFileItem> QueuedFiles => Files;

        public void InvokeHandleFileSelected(InputFileChangeEventArgs args) => HandleFileSelected(args);

        public void SetUploadingStateForTest(bool isUploading)
        {
            var field = typeof(FileUploadComponent).GetField("_isUploading", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("_isUploading field not found.");

            field.SetValue(this, isUploading);
        }
    }

    private sealed class FakeBrowserFile(string name, long size, string contentType) : IBrowserFile
    {
        private readonly byte[] _content = new byte[Math.Max(1, (int)Math.Min(size, 1024))];

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
}

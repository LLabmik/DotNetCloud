using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for <see cref="FileUploadedEventHandler"/> verifying IEventHandler contract and logging.
/// </summary>
[TestClass]
public class FileUploadedEventHandlerTests
{
    private FileUploadedEventHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _handler = new FileUploadedEventHandler(
            NullLogger<FileUploadedEventHandler>.Instance);
    }

    [TestMethod]
    public void WhenCreatedThenImplementsIEventHandler()
    {
        Assert.IsInstanceOfType<IEventHandler<FileUploadedEvent>>(_handler);
    }

    [TestMethod]
    public async Task WhenHandledThenCompletesSuccessfully()
    {
        var evt = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            Size = 1024,
            UploadedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt);
    }

    [TestMethod]
    public async Task WhenHandledWithCancellationThenCompletesSuccessfully()
    {
        var cts = new CancellationTokenSource();
        var evt = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            UploadedByUserId = Guid.NewGuid()
        };

        await _handler.HandleAsync(evt, cts.Token);
    }

    [TestMethod]
    public async Task WhenHandledThenReturnsCompletedTask()
    {
        var evt = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            UploadedByUserId = Guid.NewGuid()
        };

        var task = _handler.HandleAsync(evt);

        Assert.IsTrue(task.IsCompleted);
        await task;
    }
}

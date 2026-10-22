using DotNetCloud.Modules.Photos.Data.Services;

namespace DotNetCloud.Modules.Photos.Tests;

[TestClass]
public sealed class PhotoThumbnailQueueTests
{
    [TestMethod]
    public void TryEnqueue_WhenQueueHasCapacity_ReturnsTrueAndTracksPendingCount()
    {
        var queue = new PhotoThumbnailQueue(2);

        Assert.IsTrue(queue.TryEnqueue(new PhotoThumbnailWorkItem(Guid.CreateVersion7(), null, "image/jpeg")));
        Assert.IsTrue(queue.TryEnqueue(new PhotoThumbnailWorkItem(Guid.CreateVersion7(), null, "image/jpeg")));

        Assert.AreEqual(2, queue.PendingCount);
    }

    [TestMethod]
    public void TryEnqueue_WhenQueueIsFull_ReturnsFalse()
    {
        var queue = new PhotoThumbnailQueue(1);
        Assert.IsTrue(queue.TryEnqueue(new PhotoThumbnailWorkItem(Guid.CreateVersion7(), null, "image/jpeg")));

        Assert.IsFalse(queue.TryEnqueue(new PhotoThumbnailWorkItem(Guid.CreateVersion7(), null, "image/jpeg")));
        Assert.AreEqual(1, queue.PendingCount);
    }

    [TestMethod]
    public async Task DequeueAsync_ReturnsItemsInEnqueueOrder()
    {
        var queue = new PhotoThumbnailQueue(4);
        var first = new PhotoThumbnailWorkItem(Guid.CreateVersion7(), "files/aa/bb", "image/jpeg");
        var second = new PhotoThumbnailWorkItem(Guid.CreateVersion7(), null, "image/png");

        queue.TryEnqueue(first);
        queue.TryEnqueue(second);

        var dequeuedFirst = await queue.DequeueAsync(CancellationToken.None);
        var dequeuedSecond = await queue.DequeueAsync(CancellationToken.None);

        Assert.AreEqual(first, dequeuedFirst);
        Assert.AreEqual(second, dequeuedSecond);
        Assert.AreEqual(0, queue.PendingCount);
    }

    [TestMethod]
    public async Task DequeueAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var queue = new PhotoThumbnailQueue(1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await queue.DequeueAsync(cts.Token);
            Assert.Fail("Expected OperationCanceledException.");
        }
        catch (OperationCanceledException)
        {
            // expected
        }
    }

    [TestMethod]
    public void Constructor_NonPositiveCapacity_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new PhotoThumbnailQueue(0));
    }
}

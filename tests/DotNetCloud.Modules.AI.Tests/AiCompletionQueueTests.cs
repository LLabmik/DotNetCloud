using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotNetCloud.Core.AI;
using DotNetCloud.Modules.AI.Data.Services;
using DotNetCloud.Modules.AI.Services;

namespace DotNetCloud.Modules.AI.Tests;

/// <summary>
/// Tests for <see cref="AiCompletionQueue"/> — the FIFO serializer for LLM inference.
/// </summary>
[TestClass]
public class AiCompletionQueueTests
{
    [TestMethod]
    public async Task EnqueueStreaming_ProcessesWorkSequentially()
    {
        using var queue = new AiCompletionQueue();

        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var e1 = queue.EnqueueStreaming(
            ct => GatedChunks(firstStarted, firstRelease, ct), CancellationToken.None);
        var e2 = queue.EnqueueStreaming(
            ct => TrackingChunks(secondStarted, "two"), CancellationToken.None);

        // The first request starts; the second must not run until the first completes.
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(secondStarted.Task.IsCompleted, "Second request must wait for the first.");

        firstRelease.TrySetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await DrainAsync(e1);
        await DrainAsync(e2);
    }

    [TestMethod]
    public async Task EnqueueStreaming_ReportsPositionAndTotal()
    {
        using var queue = new AiCompletionQueue();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // First request blocks the worker; the next two queue behind it.
        var e1 = queue.EnqueueStreaming(ct => GatedChunks(started, release, ct), CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var e2 = queue.EnqueueStreaming(ct => EmptyChunks(), CancellationToken.None);
        var e3 = queue.EnqueueStreaming(ct => EmptyChunks(), CancellationToken.None);

        // e1 is in-flight (removed from the list), so the waiting entries show 1,2 of 2.
        Assert.AreEqual(1, e2.Position);
        Assert.AreEqual(2, e3.Position);
        Assert.AreEqual(2, e2.Total);
        Assert.AreEqual(2, e3.Total);

        release.TrySetResult();
        await DrainAsync(e1);
        await DrainAsync(e2);
        await DrainAsync(e3);
    }

    [TestMethod]
    public async Task EnqueueStreaming_CancelledWhileQueued_IsRemoved()
    {
        using var queue = new AiCompletionQueue();

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelledRan = false;

        var e1 = queue.EnqueueStreaming(ct => GatedChunks(started, release, ct), CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource();
        var e2 = queue.EnqueueStreaming(
            ct =>
            {
                cancelledRan = true;
                return EmptyChunks();
            },
            cts.Token);
        var e3 = queue.EnqueueStreaming(ct => EmptyChunks(), CancellationToken.None);

        Assert.AreEqual(1, e2.Position);
        Assert.AreEqual(2, e3.Position);

        // Cancelling the queued request removes it from the queue and advances the next.
        cts.Cancel();

        Assert.AreEqual(1, e3.Position);
        Assert.AreEqual(1, e3.Total);
        Assert.IsFalse(cancelledRan, "Cancelled request must never run.");

        release.TrySetResult();
        await DrainAsync(e1);
        await DrainAsync(e3);
    }

    [TestMethod]
    public async Task EnqueueTaskAsync_ReturnsResult()
    {
        using var queue = new AiCompletionQueue();

        var result = await queue.EnqueueTaskAsync(ct => Task.FromResult(42), CancellationToken.None);

        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public async Task EnqueueTaskAsync_SerializesWithStreamingWork()
    {
        using var queue = new AiCompletionQueue();

        var streamStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var streamRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var taskRan = false;

        var entry = queue.EnqueueStreaming(ct => GatedChunks(streamStarted, streamRelease, ct), CancellationToken.None);
        await streamStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var task = queue.EnqueueTaskAsync(
            ct =>
            {
                taskRan = true;
                return Task.FromResult(7);
            },
            CancellationToken.None);

        // The task must wait until the streaming request finishes.
        Assert.IsFalse(task.IsCompleted, "Task work must wait behind streaming work.");

        streamRelease.TrySetResult();
        var value = await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(7, value);
        Assert.IsTrue(taskRan);

        await DrainAsync(entry);
    }

    [TestMethod]
    public async Task EnqueueTaskAsync_CancelledWhileQueued_IsCancelled()
    {
        using var queue = new AiCompletionQueue();

        var streamStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var streamRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Block the worker with a streaming request so the task stays queued.
        var entry = queue.EnqueueStreaming(ct => GatedChunks(streamStarted, streamRelease, ct), CancellationToken.None);
        await streamStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource();
        var task = queue.EnqueueTaskAsync(ct => Task.FromResult(1), cts.Token);

        cts.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => task);

        streamRelease.TrySetResult();
        await DrainAsync(entry);
    }

    [TestMethod]
    public async Task EnqueueStreaming_AppliesCooldownBetweenItems()
    {
        using var queue = new AiCompletionQueue(TimeSpan.FromMilliseconds(200));

        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var e1 = queue.EnqueueStreaming(ct => GatedChunks(firstStarted, firstRelease, ct), CancellationToken.None);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var e2 = queue.EnqueueStreaming(ct => TrackingChunks(secondStarted, "two"), CancellationToken.None);

        var sw = Stopwatch.StartNew();
        firstRelease.TrySetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        sw.Stop();

        Assert.IsTrue(
            sw.ElapsedMilliseconds >= 150,
            $"Second request should start only after the cooldown (elapsed: {sw.ElapsedMilliseconds} ms).");

        await DrainAsync(e1);
        await DrainAsync(e2);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static async IAsyncEnumerable<LlmResponseChunk> GatedChunks(
        TaskCompletionSource started,
        TaskCompletionSource release,
        [EnumeratorCancellation] CancellationToken ct)
    {
        started.TrySetResult();
        await release.Task.WaitAsync(ct);
        yield return new LlmResponseChunk { Model = "m", Content = "hi", Done = false };
        yield return new LlmResponseChunk { Model = "m", Content = "", Done = true };
    }

    private static async IAsyncEnumerable<LlmResponseChunk> TrackingChunks(
        TaskCompletionSource started,
        string content)
    {
        started.TrySetResult();
        yield return new LlmResponseChunk { Model = "m", Content = content, Done = false };
        yield return new LlmResponseChunk { Model = "m", Content = "", Done = true };
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<LlmResponseChunk> EmptyChunks()
    {
        yield return new LlmResponseChunk { Model = "m", Content = "", Done = true };
        await Task.CompletedTask;
    }

    private static async Task DrainAsync(AiQueuedStream entry)
    {
        await foreach (var _ in entry.Reader.ReadAllAsync())
        {
        }
    }
}

using System.Threading.Channels;
using DotNetCloud.Core.AI;
using DotNetCloud.Modules.AI.Services;

namespace DotNetCloud.Modules.AI.Data.Services;

/// <summary>
/// Serializes LLM inference so only one request is in flight at a time.
/// A FIFO list guarded by a lock is the authoritative queue; a single background
/// worker processes the head of the list. Cancellation while queued removes the
/// item; cancellation while processing aborts the Ollama call.
/// </summary>
public sealed class AiCompletionQueue : IAiCompletionQueue, IDisposable
{
    private readonly object _gate = new();
    private readonly List<QueueItem> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TimeSpan _cooldown;
    private readonly Task _worker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiCompletionQueue"/> class
    /// and starts the background processing loop.
    /// </summary>
    /// <param name="cooldown">
    /// Optional delay between finishing one item and starting the next, so the LLM
    /// provider fully releases the previous generation before the next request.
    /// Defaults to 500 ms.
    /// </param>
    public AiCompletionQueue(TimeSpan? cooldown = null)
    {
        _cooldown = cooldown ?? TimeSpan.FromMilliseconds(500);
        _worker = Task.Run(ProcessLoopAsync);
    }

    /// <inheritdoc />
    public AiQueuedStream EnqueueStreaming(
        Func<CancellationToken, IAsyncEnumerable<LlmResponseChunk>> work,
        CancellationToken cancellationToken)
    {
        var item = new QueueItem
        {
            Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
            Work = work,
            Out = Channel.CreateUnbounded<LlmResponseChunk>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true })
        };
        Enqueue(item);
        return new AiQueuedStream(item.Out.Reader, () => item.Position, () => item.Total);
    }

    /// <inheritdoc />
    public Task<TResult> EnqueueTaskAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var item = new QueueItem
        {
            Cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
            TaskWork = async token =>
            {
                try
                {
                    tcs.TrySetResult(await work(token).ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }
        };

        // If the caller cancels while queued, the item is removed and never runs —
        // complete the returned task with cancellation so the caller doesn't hang.
        item.Cts.Token.Register(() => tcs.TrySetCanceled(item.Cts.Token));

        Enqueue(item);
        return tcs.Task;
    }

    private void Enqueue(QueueItem item)
    {
        lock (_gate)
        {
            _queue.Add(item);
            RecomputeLocked();
            _signal.Release();
        }

        // Remove from the queue if the caller cancels before its turn.
        item.Cts.Token.Register(() =>
        {
            lock (_gate)
            {
                if (!item.Started && _queue.Remove(item))
                {
                    RecomputeLocked();
                }
            }
        });
    }

    private void RecomputeLocked()
    {
        for (var i = 0; i < _queue.Count; i++)
        {
            _queue[i].Position = i + 1;
            _queue[i].Total = _queue.Count;
        }
    }

    private async Task ProcessLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(_shutdown.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            QueueItem? item;
            lock (_gate)
            {
                // Skip already-cancelled items at the head.
                while (_queue.Count > 0 && !_queue[0].Started && _queue[0].Cts.IsCancellationRequested)
                {
                    _queue.RemoveAt(0);
                }

                if (_queue.Count == 0)
                {
                    continue;
                }

                item = _queue[0];
                _queue.RemoveAt(0);
                item.Started = true;
                RecomputeLocked();
            }

            try
            {
                if (item.Out is not null)
                {
                    // Generating marker, then content chunks.
                    await item.Out.Writer.WriteAsync(new LlmResponseChunk
                    {
                        Model = string.Empty,
                        Content = string.Empty,
                        Status = LlmStreamStatus.Generating,
                        Done = false
                    }, item.Cts.Token);

                    await foreach (var chunk in item.Work!(item.Cts.Token))
                    {
                        await item.Out.Writer.WriteAsync(chunk, item.Cts.Token);
                    }
                }
                else
                {
                    await item.TaskWork!(item.Cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled — nothing to do.
            }
            catch (Exception ex)
            {
                // Log is not available here; fail the stream gracefully with the error.
                item.Out?.Writer.TryComplete(ex);
            }
            finally
            {
                item.Out?.Writer.TryComplete();
            }

            // Small cooldown between requests so the LLM provider fully releases the
            // previous generation before the next one starts (avoids back-to-back
            // teardown races that can leave the provider in a stuck state).
            try
            {
                await Task.Delay(_cooldown, _shutdown.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
        _signal.Dispose();
    }

    private sealed class QueueItem
    {
        public CancellationTokenSource Cts { get; init; } = null!;
        public bool Started;
        public int Position = 1;
        public int Total = 1;
        public Channel<LlmResponseChunk>? Out;
        public Func<CancellationToken, IAsyncEnumerable<LlmResponseChunk>>? Work;
        public Func<CancellationToken, Task>? TaskWork;
    }
}

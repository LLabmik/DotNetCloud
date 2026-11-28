using System.Threading.Channels;
using DotNetCloud.Core.AI;

namespace DotNetCloud.Modules.AI.Services;

/// <summary>
/// FIFO queue that serializes LLM inference so only one Ollama request runs at a time.
/// Shared by all callers in the AI module host process.
/// </summary>
public interface IAiCompletionQueue
{
    /// <summary>
    /// Enqueues a streaming inference. Returns immediately with an entry exposing
    /// live <see cref="AiQueuedStream.Position"/>/<see cref="AiQueuedStream.Total"/>
    /// and the serialized result stream (a Generating marker, then content, then Done).
    /// </summary>
    AiQueuedStream EnqueueStreaming(
        Func<CancellationToken, IAsyncEnumerable<LlmResponseChunk>> work,
        CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues non-streaming work and returns the result when the turn completes.
    /// </summary>
    Task<TResult> EnqueueTaskAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken);
}

/// <summary>
/// Handle returned by <see cref="IAiCompletionQueue.EnqueueStreaming"/>.
/// Exposes the serialized result stream plus live queue-position accessors.
/// </summary>
public sealed class AiQueuedStream
{
    private readonly Func<int> _position;
    private readonly Func<int> _total;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiQueuedStream"/> class.
    /// </summary>
    /// <param name="reader">The serialized result stream (Generating marker, then content, then Done).</param>
    /// <param name="position">Accessor for the current 1-based queue position.</param>
    /// <param name="total">Accessor for the current total queue length.</param>
    public AiQueuedStream(ChannelReader<LlmResponseChunk> reader, Func<int> position, Func<int> total)
    {
        Reader = reader;
        _position = position;
        _total = total;
    }

    /// <summary>Serialized result stream. First item is a Generating marker.</summary>
    public ChannelReader<LlmResponseChunk> Reader { get; }

    /// <summary>Current 1-based queue position (1 = next in line).</summary>
    public int Position => _position();

    /// <summary>Current total queue length (waiting + in-flight).</summary>
    public int Total => _total();
}

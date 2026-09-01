# AI Request Queuing Implementation Plan

> Target branch: `feature/ai-queuing`
>
> Status: **Ready for implementation**
>
> Scope: Serialize all AI chat inference through a single FIFO queue (Ollama handles one
> request at a time), stream live queue-position updates to both clients ("Your request is
> in queue position 3 of 8"), and remove user-facing model selection (admin `DefaultModel`
> only, shown as static text).

---

## 1. Goals

1. **One-at-a-time inference, globally.** All AI chat requests — from Blazor and Android —
   are funneled through a single FIFO queue owned by the `dotnetcloud.ai` module host.
2. **Live queue feedback.** Clients see `Queued (position X of Y)` → `Generating…` → streamed
   tokens, pushed over the existing SSE (REST) and gRPC streaming channels.
3. **No model choice for users.** The model selector is removed from Blazor and Android.
   `CreateConversation` always uses the admin `DefaultModel` from the `dbo.SystemSettings`
   table (module `dotnetcloud.ai`, key `DefaultModel`). The model is displayed as static text.

## 2. Locked decisions (from user)

- Queue is **centralized in the AI module host** (single choke point).
- Blazor is **routed through the existing gRPC `IAiApiClient`** (currently registered but
  unused by the AI page). The in-process `AddAiUiServices` chat path is removed.
- Queue position is **pushed live** over the existing stream (no polling endpoints).
- A **cancelled/disconnected request is removed** from the queue.

## 3. Current vs target data flow

**Current**

```
Blazor (Core.Server) ──in-process──> AiChatService ──HTTP──> Ollama
Android ──REST──> Core.Server (YARP proxy /api/v1/ai) ──> dotnetcloud.ai host ──> AiChatService ──HTTP──> Ollama
```

Two processes can hit Ollama concurrently.

**Target**

```
Blazor (Core.Server) ──gRPC IAiApiClient──> dotnetcloud.ai host
                                            └─> AiChatService ──> AiCompletionQueue (singleton, FIFO) ──> OllamaClient ──HTTP──> Ollama
Android ──REST──> Core.Server (YARP) ──> dotnetcloud.ai host ──> AiChatController ──> AiChatService ──> same queue
```

Both clients enter the **same** singleton queue in the module host process.

---

## 4. Part 1 — Core contracts (`DotNetCloud.Core.AI`)

### 4.1 Add `LlmStreamStatus` enum

File: `src/Core/DotNetCloud.Core/AI/LlmStreamStatus.cs` (new)

```csharp
namespace DotNetCloud.Core.AI;

/// <summary>
/// Lifecycle status of a streamed LLM response chunk.
/// </summary>
public enum LlmStreamStatus
{
    /// <summary>No status (default).</summary>
    Unknown = 0,

    /// <summary>The request is waiting in the inference queue.</summary>
    Queued = 1,

    /// <summary>The model is actively generating tokens.</summary>
    Generating = 2,

    /// <summary>Generation finished.</summary>
    Done = 3
}
```

### 4.2 Extend `LlmResponseChunk`

File: `src/Core/DotNetCloud.Core/AI/LlmResponseChunk.cs`

Current record:

```csharp
public sealed record LlmResponseChunk
{
    public required string Model { get; init; }
    public required string Content { get; init; }
    public bool Done { get; init; }
    public long? TotalDurationNs { get; init; }
    public int? EvalCount { get; init; }
}
```

New record (add three properties; keep the rest):

```csharp
public sealed record LlmResponseChunk
{
    public required string Model { get; init; }
    public required string Content { get; init; }
    public bool Done { get; init; }
    public long? TotalDurationNs { get; init; }
    public int? EvalCount { get; init; }

    /// <summary>Stream lifecycle status. Defaults to Generating (existing content chunks).</summary>
    public LlmStreamStatus Status { get; init; } = LlmStreamStatus.Generating;

    /// <summary>1-based queue position (only on Queued status chunks).</summary>
    public int? QueuedPosition { get; init; }

    /// <summary>Total items in the queue (only on Queued status chunks).</summary>
    public int? QueueTotal { get; init; }
}
```

`OllamaClient` is **not modified** — its content chunks keep the default `Status = Generating`,
and its final chunk keeps `Done = true`.

---

## 5. Part 2 — Queue service (new)

### 5.1 Interface

File: `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiCompletionQueue.cs` (new)

```csharp
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
    /// live <see cref="AiQueueStreamEntry.Position"/>/<see cref="AiQueueStreamEntry.Total"/>
    /// and the serialized result stream (a Generating marker, then content, then Done).
    /// </summary>
    AiQueueStreamEntry EnqueueStreaming(
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
/// </summary>
public sealed class AiQueueStreamEntry
{
    private readonly QueueItem _item;

    internal AiQueueStreamEntry(QueueItem item)
    {
        _item = item;
    }

    /// <summary>Current 1-based queue position (1 = next in line).</summary>
    public int Position => _item.Position;

    /// <summary>Current total queue length (waiting + in-flight).</summary>
    public int Total => _item.Total;

    /// <summary>Serialized result stream. First item is a Generating marker.</summary>
    public ChannelReader<LlmResponseChunk> Reader => _item.Out!.Reader;
}
```

> `QueueItem` is the internal class defined in the implementation file. To keep the
> interface public surface clean, either expose `Position`/`Total` as plain `int` properties
> backed by the item, or make `AiQueueStreamEntry` internal and have the interface return the
> reader + a small status reader instead. The **simplest workable approach**: make
> `AiQueueStreamEntry` an internal class in the same assembly as the implementation, and have
> `IAiCompletionQueue.EnqueueStreaming` return a small public record
> `AiQueuedStream(ChannelReader<LlmResponseChunk> Reader, Func<int> Position, Func<int> Total)`.
> Implementer may choose either shape — only the three values (reader, position, total) matter.

### 5.2 Implementation

File: `src/Modules/AI/DotNetCloud.Modules.AI.Data/Services/AiCompletionQueue.cs` (new)

Design:

- A `List<QueueItem> _queue` guarded by `object _gate` is the **authoritative FIFO**.
- A single background worker (`Task.Run`) processes the head of the list one at a time.
- A `SemaphoreSlim _signal` wakes the worker when a new item is enqueued.
- Each item has a `CancellationTokenSource` linked to the caller token, so cancellation
  while queued removes the item and cancellation while processing aborts the Ollama call.
- `Position`/`Total` are recomputed for all items under the lock on every enqueue/dequeue.

```csharp
using System.Threading.Channels;
using DotNetCloud.Core.AI;
using DotNetCloud.Modules.AI.Services;

namespace DotNetCloud.Modules.AI.Data.Services;

/// <summary>
/// Serializes LLM inference so only one request is in flight at a time.
/// </summary>
public sealed class AiCompletionQueue : IAiCompletionQueue, IDisposable
{
    private readonly object _gate = new();
    private readonly List<QueueItem> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _worker;

    public AiCompletionQueue()
    {
        _worker = Task.Run(ProcessLoopAsync);
    }

    /// <inheritdoc />
    public AiQueueStreamEntry EnqueueStreaming(
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
        return new AiQueueStreamEntry(item);
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
                try { tcs.TrySetResult(await work(token).ConfigureAwait(false)); }
                catch (Exception ex) { tcs.TrySetException(ex); }
            }
        };
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
                    RecomputeLocked();
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
            await _signal.WaitAsync(_shutdown.Token);

            QueueItem? item;
            lock (_gate)
            {
                // Skip already-cancelled items at the head.
                while (_queue.Count > 0 && !_queue[0].Started && _queue[0].Cts.IsCancellationRequested)
                    _queue.RemoveAt(0);

                if (_queue.Count == 0)
                    continue;

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
                        await item.Out.Writer.WriteAsync(chunk, item.Cts.Token);
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
                // Log and fail the stream gracefully.
                item.Out?.Writer.TryComplete(ex);
            }
            finally
            {
                item.Out?.Writer.TryComplete();
            }
        }
    }

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
```

> The `AiQueueStreamEntry` type is referenced above; define it in
> `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiCompletionQueue.cs` (or a sibling file
> in the same namespace) so both the interface and the Data implementation can use it.
> Its `QueueItem` reference above can be replaced with storing `Func<int>` accessors, or by
> making the entry internal to the Data assembly and exposing the three values via the
> interface instead. Pick the simplest arrangement that compiles in this repo's
> `interface in .AI / impl in .Data` split.

### 5.3 Register the queue

File: `src/Modules/AI/DotNetCloud.Modules.AI.Data/AiServiceRegistration.cs`

In `AddAiServices` (the method used by the module host), add:

```csharp
services.AddSingleton<IAiCompletionQueue, AiCompletionQueue>();
```

Do **not** register it in `AddAiUiServices` (that method is being removed from Core.Server —
see Part 6).

---

## 6. Part 3 — `AiChatService` changes

### 6.1 Interface

File: `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiChatService.cs`

Change the `CreateConversationAsync` signature (remove the `model` parameter):

```csharp
Task<Conversation> CreateConversationAsync(
    CallerContext caller, string? title, string? systemPrompt, CancellationToken cancellationToken = default);
```

Leave the other methods unchanged. Optionally remove `ListModelsAsync` from the interface if
it becomes fully unused (it is currently only called by the model pickers being removed).
**Keep it for now** — the RPC/REST endpoint remain for admin/future use.

### 6.2 Implementation

File: `src/Modules/AI/DotNetCloud.Modules.AI.Data/Services/AiChatService.cs`

**Constructor** — add two dependencies:

```csharp
private readonly AiDbContext _db;
private readonly IOllamaClient _ollamaClient;
private readonly IAiCompletionQueue _queue;
private readonly IAiSettingsProvider _settingsProvider;
private readonly IAuditLogger _auditLogger;
private readonly ILogger<AiChatService> _logger;

public AiChatService(
    AiDbContext db,
    IOllamaClient ollamaClient,
    IAiCompletionQueue queue,
    IAiSettingsProvider settingsProvider,
    IAuditLogger auditLogger,
    ILogger<AiChatService> logger)
```

**`CreateConversationAsync`** — resolve the default model internally:

```csharp
public async Task<Conversation> CreateConversationAsync(
    CallerContext caller, string? title, string? systemPrompt, CancellationToken cancellationToken = default)
{
    var model = await _settingsProvider.GetDefaultModelAsync(cancellationToken);
    // ... rest identical to today, using `model` for Conversation.Model ...
}
```

**`SendMessageAsync` (non-streaming)** — run the Ollama call through the queue:

```csharp
var response = await _queue.EnqueueTaskAsync(
    ct => _ollamaClient.ChatAsync(llmRequest, ct),
    cancellationToken);
```

Everything else (persist user message, build request, persist assistant response) stays the
same, with the queue call replacing the direct `_ollamaClient.ChatAsync` call.

**`SendMessageStreamingAsync`** — persist the user message and build the request first
(exactly as today), then enqueue the streaming call and interleave queue-status chunks while
waiting for the turn:

```csharp
// (existing: load conversation, persist user message, SaveChanges, BuildLlmRequest)

var entry = _queue.EnqueueStreaming(
    ct => _ollamaClient.ChatStreamingAsync(llmRequest, ct),
    cancellationToken);

var reader = entry.Reader;
var fullContent = new System.Text.StringBuilder();
int? evalCount = null;

while (!cancellationToken.IsCancellationRequested)
{
    var readTask = reader.WaitToReadAsync(cancellationToken).AsTask();
    var delayTask = Task.Delay(500, cancellationToken);
    var completed = await Task.WhenAny(readTask, delayTask);

    if (completed == readTask && await readTask)
    {
        while (reader.TryRead(out var chunk))
        {
            if (chunk.Status == LlmStreamStatus.Queued)
            {
                yield return chunk; // queue position update
                continue;
            }

            fullContent.Append(chunk.Content);
            if (chunk.Done)
                evalCount = chunk.EvalCount;

            yield return chunk;
        }
        break; // channel completed
    }
    else
    {
        // Still waiting for the turn — report current position.
        yield return new LlmResponseChunk
        {
            Model = llmRequest.Model,
            Content = string.Empty,
            Status = LlmStreamStatus.Queued,
            Done = false,
            QueuedPosition = entry.Position,
            QueueTotal = entry.Total
        };
    }
}

// (existing: persist assistant response, auto-title, update timestamp, SaveChanges)
```

> Note: the `Generating` marker emitted by the queue worker has `Status = Generating` and
> `Content = ""`; forward it as-is (clients use it to switch from "queued" to "thinking").

---

## 7. Part 4 — gRPC contract & service

### 7.1 Proto

File: `src/Modules/AI/DotNetCloud.Modules.AI.Host/Protos/ai_service.proto`

1. **Remove** `string model = 3;` from `CreateConversationRequest`.
2. **Add** the status enum and new `MessageChunk` fields:

```proto
enum AiStreamStatus {
  AI_STREAM_STATUS_UNKNOWN = 0;
  AI_STREAM_STATUS_QUEUED = 1;
  AI_STREAM_STATUS_GENERATING = 2;
  AI_STREAM_STATUS_DONE = 3;
}

message MessageChunk {
  string content = 1;
  bool done = 2;
  int32 eval_count = 3;
  AiStreamStatus status = 4;
  int32 queued_position = 5;
  int32 queue_total = 6;
}
```

3. **Add** a health RPC and its messages:

```proto
rpc GetOllamaHealth (GetOllamaHealthRequest) returns (OllamaHealthResponse);

message GetOllamaHealthRequest { string user_id = 1; }
message OllamaHealthResponse { bool healthy = 1; }
```

The C# types are regenerated by `dotnet build` (Grpc.Tools). No manual generation.

### 7.2 gRPC service

File: `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/AiGrpcService.cs`

1. **Constructor** — inject `IOllamaClient` (registered in the host via `AddAiServices`):

```csharp
public AiGrpcService(
    IAiChatService chatService,
    IAiSettingsProvider settingsProvider,
    IOllamaClient ollamaClient,
    ILogger<AiGrpcService> logger)
```

2. **Replace every `CallerContext.CreateSystemContext()`** with a real user context parsed from
   the request's `user_id`:

```csharp
private static CallerContext ToCaller(string? userId) =>
    Guid.TryParse(userId, out var id) && id != Guid.Empty
        ? new CallerContext(id, Array.Empty<string>(), CallerType.User)
        : CallerContext.CreateSystemContext();
```

Apply in: `CreateConversation`, `GetConversation`, `ListConversations`,
`DeleteConversation`, `RenameConversation`, `SendMessage`, `SendMessageStream`.
(Ownership filtering in `AiChatService` depends on the correct `UserId`.)

3. **`CreateConversation`** — drop the model logic and call the new signature:

```csharp
var conversation = await _chatService.CreateConversationAsync(
    caller,
    string.IsNullOrWhiteSpace(request.Title) ? null : request.Title,
    string.IsNullOrWhiteSpace(request.SystemPrompt) ? null : request.SystemPrompt,
    context.CancellationToken);
```

4. **`SendMessageStream`** — map the new status fields:

```csharp
await foreach (var chunk in _chatService.SendMessageStreamingAsync(
    ToCaller(request.UserId), Guid.Parse(request.ConversationId), request.Message, context.CancellationToken))
{
    await stream.WriteAsync(new MessageChunk
    {
        Content = chunk.Content,
        Done = chunk.Done,
        EvalCount = chunk.EvalCount ?? 0,
        Status = (AiStreamStatus)(int)chunk.Status,
        QueuedPosition = chunk.QueuedPosition ?? 0,
        QueueTotal = chunk.QueueTotal ?? 0
    });
}
```

5. **Add `GetOllamaHealth`**:

```csharp
public override async Task<OllamaHealthResponse> GetOllamaHealth(
    GetOllamaHealthRequest request, ServerCallContext context)
{
    var healthy = await _ollamaClient.IsHealthyAsync(context.CancellationToken);
    return new OllamaHealthResponse { Healthy = healthy };
}
```

> Note: the generated enum member names for `AiStreamStatus` will be
> `Unknown`, `Queued`, `Generating`, `Done` (grpc-dotnet strips the
> `AI_STREAM_STATUS_` prefix). Cast `(AiStreamStatus)(int)chunk.Status` to be safe.

---

## 8. Part 5 — REST controller (Android path)

File: `src/Modules/AI/DotNetCloud.Modules.AI.Host/Controllers/AiChatController.cs`

1. **`CreateConversationRequest` DTO** — remove `Model`:

```csharp
public sealed class CreateConversationRequest
{
    public string? Title { get; set; }
    public string? SystemPrompt { get; set; }
}
```

2. **`CreateConversation`** action — call the new signature (no model):

```csharp
var conversation = await _chatService.CreateConversationAsync(
    caller, request.Title, request.SystemPrompt, cancellationToken);
```

Delete the `_settingsProvider.GetDefaultModelAsync` / `model` lines.

3. **`SendMessage`** action — the service already queues internally; no controller change
   needed beyond it continuing to call `SendMessageAsync`.

4. **`StreamMessage`** — emit status fields in each SSE event. New JSON shape:
   - Queued: `{"status":"queued","position":3,"total":8,"content":"","done":false,"evalCount":null}`
   - Generating marker / content: `{"status":"generating","content":"...","done":false,"evalCount":null}`
   - Done: `{"status":"done","content":"","done":true,"evalCount":12}`
   - Sentinel: `data: [DONE]`

   Implementation sketch:

```csharp
await foreach (var chunk in _chatService.SendMessageStreamingAsync(caller, conversationId, request.Message, cancellationToken))
{
    var data = System.Text.Json.JsonSerializer.Serialize(new
    {
        status = chunk.Status switch
        {
            LlmStreamStatus.Queued => "queued",
            LlmStreamStatus.Done => "done",
            _ => "generating"
        },
        position = chunk.QueuedPosition,
        total = chunk.QueueTotal,
        content = chunk.Content,
        done = chunk.Done,
        evalCount = chunk.EvalCount
    });

    await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
    await Response.Body.FlushAsync(cancellationToken);
}
```

5. **Add `GET /api/v1/ai/settings`** — so Android can show the default model as static text:

```csharp
[HttpGet("settings")]
public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
{
    return Ok(new
    {
        defaultModel = await _settingsProvider.GetDefaultModelAsync(cancellationToken),
        provider = await _settingsProvider.GetProviderAsync(cancellationToken)
    });
}
```

---

## 9. Part 6 — Core.Server: gRPC client & DI

### 9.1 `IAiApiClient` interface

File: `src/Core/DotNetCloud.Core/Services/ModuleApis/IAiApiClient.cs`

Mirror the existing `IChatApiClient` convention: add `Guid userId` to every method that needs
per-user data, remove `model` from `CreateConversationAsync`, add `IsOllamaHealthyAsync`, and
extend `MessageChunkDto`.

Target signatures:

```csharp
Task<ConversationDto?> CreateConversationAsync(Guid userId, string? title, string? systemPrompt, CancellationToken ct = default);
Task<ConversationDetailDto?> GetConversationAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(Guid userId, CancellationToken ct = default);
Task<bool> DeleteConversationAsync(Guid userId, Guid conversationId, CancellationToken ct = default);
Task<bool> RenameConversationAsync(Guid userId, Guid conversationId, string newTitle, CancellationToken ct = default);
Task<ChatResponseDto?> SendMessageAsync(Guid userId, Guid conversationId, string message, CancellationToken ct = default);
IAsyncEnumerable<MessageChunkDto> SendMessageStreamingAsync(Guid userId, Guid conversationId, string message, CancellationToken ct = default);
Task<bool> IsOllamaHealthyAsync(CancellationToken ct = default);
// keep: ListModelsAsync, GetSettingsAsync, UpdateSettingsAsync (unchanged)
```

`MessageChunkDto` gains status fields (reuse `DotNetCloud.Core.AI.LlmStreamStatus`):

```csharp
public sealed record MessageChunkDto
{
    public string Content { get; init; } = "";
    public bool Done { get; init; }
    public int EvalCount { get; init; }
    public LlmStreamStatus Status { get; init; } = LlmStreamStatus.Generating;
    public int? QueuedPosition { get; init; }
    public int? QueueTotal { get; init; }
}
```

### 9.2 `AiGrpcApiClient`

File: `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AiGrpcApiClient.cs`

1. Update all methods to accept and send `userId` (`UserId = userId.ToString()`), matching the
   interface. Example for `ListConversationsAsync`:

```csharp
var request = new ListConversationsRequest { UserId = userId.ToString() };
```

2. `CreateConversationAsync` — no `model` in the request:

```csharp
var request = new CreateConversationRequest { UserId = userId.ToString(), Title = title ?? "", SystemPrompt = systemPrompt ?? "" };
```

3. `SendMessageStreamingAsync` — map status fields and fix the deadline. The current
   `DeadlineHeaders(ct)` imposes a 30s absolute deadline, which is too short for queue waits +
   generation. Use a long streaming deadline:

```csharp
public async IAsyncEnumerable<MessageChunkDto> SendMessageStreamingAsync(
    Guid userId, Guid conversationId, string message, [EnumeratorCancellation] CancellationToken ct = default)
{
    var request = new SendMessageRequest { ConversationId = conversationId.ToString(), UserId = userId.ToString(), Message = message };
    using var call = _client.Value.SendMessageStream(request, new CallOptions(
        deadline: DateTime.UtcNow.Add(_options.StreamTimeout),
        cancellationToken: ct));
    await foreach (var chunk in call.ResponseStream.ReadAllAsync(ct))
    {
        yield return new MessageChunkDto
        {
            Content = chunk.Content,
            Done = chunk.Done,
            EvalCount = chunk.EvalCount,
            Status = (LlmStreamStatus)(int)chunk.Status,
            QueuedPosition = chunk.QueuedPosition > 0 ? chunk.QueuedPosition : null,
            QueueTotal = chunk.QueueTotal > 0 ? chunk.QueueTotal : null
        };
    }
}
```

4. Add `StreamTimeout` to `AiGrpcClientOptions`:

```csharp
public TimeSpan StreamTimeout { get; set; } = TimeSpan.FromMinutes(30);
```

5. Add `IsOllamaHealthyAsync`:

```csharp
public async Task<bool> IsOllamaHealthyAsync(CancellationToken ct = default)
    => (await SafeCall(async () =>
    {
        var resp = await _client.Value.GetOllamaHealthAsync(
            new GetOllamaHealthRequest { UserId = Guid.Empty.ToString() }, DeadlineHeaders(ct)).ResponseAsync;
        return resp.Healthy;
    }, "GetOllamaHealth", false))!;
```

### 9.3 `Program.cs` DI

File: `src/Core/DotNetCloud.Core.Server/Program.cs`

The AI chat page no longer runs in-process. **Remove** this line (~line 339):

```csharp
builder.Services.AddAiUiServices(builder.Configuration!, provider, connectionString);
```

**Add** an explicit AI `DbContext` registration so `DbContextSchemaProvider` can still create
the `dotnetcloud.ai` schema (it resolves the DbContext from DI and skips gracefully if missing).
Mirror the existing Calendar pattern. Add right after the Calendar block (or where the removed
line was):

```csharp
// AI DbContext for schema creation by DbContextSchemaProvider only.
// AI chat goes through the process-isolated module host via IAiApiClient (gRPC).
builder.Services.AddDbContext<AiDbContext>(options =>
    ModuleDbContextConfiguration.Configure(options, provider, connectionString, "DotNetCloud.Modules.AI.Data.SqlServer"),
    ServiceLifetime.Transient);
```

`using DotNetCloud.Modules.AI.Data;` is already present at the top of `Program.cs`.

Keep this line (~588) unchanged:

```csharp
builder.Services.AddScoped<DotNetCloud.Core.Services.ModuleApis.IAiApiClient, DotNetCloud.Core.Server.Grpc.Clients.AiGrpcApiClient>();
```

> Verify nothing else in Core.Server injects `IAiChatService`, `IOllamaClient`, or
> `IAiSettingsProvider` (a repo-wide search showed only the removed `AddAiUiServices` and the
> `AiChatPage` component, which is being rewritten).

---

## 10. Part 7 — Blazor `AiChatPage.razor`

File: `src/Modules/AI/DotNetCloud.Modules.AI/UI/AiChatPage.razor`

### 10.1 Injections / usings

- **Remove** `@inject IAiChatService ChatService`, `@inject IOllamaClient OllamaClient`,
  and (if unused) `@using DotNetCloud.Modules.AI.Models`, `@using DotNetCloud.Modules.AI.Services`.
- **Add**:

```razor
@using DotNetCloud.Core.Services.ModuleApis
@inject IAiApiClient AiApi
```

### 10.2 State changes

- `_conversations`: `List<ConversationSummaryDto>`
- `_activeConversation`: `ConversationDetailDto?`
- `_activeMessages`: `List<MessageDto>`
- **Remove** `_models`, `_selectedModel`.
- **Add** `_defaultModel` (string), `_isQueued` (bool), `_queuePosition` (int), `_queueTotal` (int).

### 10.3 Sidebar — replace model dropdown with static text

Replace the `ai-model-select` block with:

```razor
<div class="ai-model-label">Model: @_defaultModel</div>
```

(Add a small CSS rule in `AiChatPage.razor.css` for `.ai-model-label` — muted text, same
padding as the old select.)

### 10.4 `OnInitializedAsync`

```csharp
_caller = await GetCallerContextAsync();
if (_caller is null) return;

_isLoading = true;
try
{
    var userId = _caller.UserId;
    var convsTask = AiApi.ListConversationsAsync(userId, CancellationToken.None);
    var healthTask = AiApi.IsOllamaHealthyAsync(CancellationToken.None);
    var settingsTask = AiApi.GetSettingsAsync(CancellationToken.None);

    await Task.WhenAll(convsTask, healthTask, settingsTask);

    _conversations = [.. await convsTask];
    _ollamaHealthy = await healthTask;
    _defaultModel = (await settingsTask)?.DefaultModel ?? string.Empty;
}
catch (Exception ex)
{
    Logger.LogError(ex, "Failed to initialize AI assistant page");
    _errorMessage = "Failed to load AI assistant. Check connectivity.";
}
finally { _isLoading = false; }
```

### 10.5 Methods — add `userId` and DTO mapping

- `StartNewConversation`: `var conv = await AiApi.CreateConversationAsync(userId, "New Chat", null);`
  then insert `ConversationSummaryDto` built from `conv` into `_conversations`.
- `LoadConversationAsync`: `var detail = await AiApi.GetConversationAsync(userId, conversationId);`
  set `_activeConversation = detail; _activeMessages = [.. detail.Messages.OrderBy(m => m.CreatedAt)];`
- `DeleteConversationAsync` / `RenameConversationAsync`: pass `userId`; update list by id.
- `SendMessageAsync`: see below.

### 10.6 `SendMessageAsync` — queue status handling

```csharp
private async Task SendMessageAsync()
{
    if (_caller is null || _activeConversation is null || string.IsNullOrWhiteSpace(_userInput)) return;

    var userMessage = _userInput.Trim();
    _userInput = string.Empty;
    _errorMessage = null;

    // Show user message immediately.
    _activeMessages.Add(new MessageDto { Id = Guid.NewGuid(), Role = "user", Content = userMessage, CreatedAt = DateTime.UtcNow });

    _isStreaming = true;
    _isQueued = true;
    _isModelLoading = false;
    _streamingContent = string.Empty;
    StateHasChanged();

    try
    {
        var fullResponse = new StringBuilder();
        var firstGenerating = true;

        await foreach (var chunk in AiApi.SendMessageStreamingAsync(_caller.UserId, _activeConversation.Id, userMessage, CancellationToken.None))
        {
            if (chunk.Status == LlmStreamStatus.Queued)
            {
                _isQueued = true;
                _queuePosition = chunk.QueuedPosition ?? _queuePosition;
                _queueTotal = chunk.QueueTotal ?? _queueTotal;
                StateHasChanged();
                continue;
            }

            if (firstGenerating)
            {
                firstGenerating = false;
                _isQueued = false;
                _modelLoadCts?.Cancel();
                _ = DetectModelLoadingAsync(_modelLoadCts = new CancellationTokenSource());
            }

            fullResponse.Append(chunk.Content);
            _streamingContent = fullResponse.ToString();
            StateHasChanged();
            await Task.Delay(1);
        }

        _activeMessages.Add(new MessageDto { Id = Guid.NewGuid(), Role = "assistant", Content = fullResponse.ToString(), CreatedAt = DateTime.UtcNow });

        if (_activeMessages.Count(m => m.Role == "user") == 1)
        {
            var titlePreview = userMessage.Length > 60 ? userMessage[..60].TrimEnd() + "…" : userMessage;
            // update _activeConversation.Title and the list item (same logic as today)
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Failed to send message");
        _errorMessage = "Failed to get response from AI.";
    }
    finally
    {
        _isStreaming = false;
        _isQueued = false;
        _isModelLoading = false;
        _streamingContent = string.Empty;
        _modelLoadCts?.Cancel();
        StateHasChanged();
    }
}
```

> `DetectModelLoadingAsync` should only run once the request leaves the queue (first
> non-queued chunk), exactly as above — do not start it while queued.

### 10.7 Streaming bubble — queue status UI

Inside the existing `@if (_isStreaming)` block, before the "Thinking" / model-loading
indicators, add:

```razor
@if (_isQueued)
{
    <div class="ai-queue-status">
        In queue: position @_queuePosition of @_queueTotal
    </div>
}
```

And wrap the existing "Thinking" text so it only shows when `!_isQueued`:

```razor
@if (_isStreaming && !_isModelLoading && !_isQueued && string.IsNullOrEmpty(_streamingContent))
{
    <span class="ai-thinking">* Thinking *</span>
}
```

Also gate the model-loading block with `!_isQueued`.

---

## 11. Part 8 — Android client

### 11.1 DTOs

File: `src/Clients/DotNetCloud.Client.Android/Ai/AiDtos.cs`

Extend `AiStreamChunk`:

```csharp
public sealed record AiStreamChunk
{
    public string Content { get; init; } = "";
    public bool Done { get; init; }
    public int? EvalCount { get; init; }

    /// <summary>"queued", "generating", or "done".</summary>
    public string? Status { get; init; }

    /// <summary>1-based queue position (present when Status == "queued").</summary>
    public int? Position { get; init; }

    /// <summary>Total queue length (present when Status == "queued").</summary>
    public int? Total { get; init; }
}
```

Add a settings DTO:

```csharp
public sealed record AiSettingsDto
{
    public string DefaultModel { get; init; } = "";
    public string Provider { get; init; } = "";
}
```

### 11.2 REST client

File: `src/Clients/DotNetCloud.Client.Android/Ai/IAiRestClient.cs`

- Change `CreateConversationAsync(string serverBaseUrl, string accessToken, string? title, string model, CancellationToken ct = default)` → drop `model`.
- Add `Task<AiSettingsDto?> GetSettingsAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);`

File: `src/Clients/DotNetCloud.Client.Android/Ai/HttpAiRestClient.cs`

- `CreateConversationAsync`: serialize `new { title }` (no model).
- Add `GetSettingsAsync` calling `GET /api/v1/ai/settings` via `GetEnvelopeDataAsync<AiSettingsDto>`.
- `SendMessageStreamingAsync`: no change needed (JSON deserialization is case-insensitive and
  ignores unknown fields; the new `status`/`position`/`total` fields populate automatically).

### 11.3 ViewModel

File: `src/Clients/DotNetCloud.Client.Android/ViewModels/AiViewModel.cs`

**Remove:** `Models` (ObservableCollection), `SelectedModelDto`, `SelectedModel`, and all
model-selection logic.

**Add observable properties:**

```csharp
[ObservableProperty] private string _defaultModel = "";
[ObservableProperty] private string _activeConversationModel = "";
[ObservableProperty] private bool _isQueued;
[ObservableProperty] private int _queuePosition;
[ObservableProperty] private int _queueTotal;

/// <summary>"In queue: position 3 of 8" when queued, else empty.</summary>
public string QueueStatusText => IsQueued ? $"In queue: position {QueuePosition} of {QueueTotal}" : "";
```

**`LoadAsync`:** replace the models+health block with conversations + health + settings:

```csharp
var healthTask = _ai.GetOllamaHealthAsync(serverUrl, token);
var settingsTask = _ai.GetSettingsAsync(serverUrl, token);
await Task.WhenAll(healthTask, settingsTask);
healthy = await healthTask;
var settings = await settingsTask;
DefaultModel = settings?.DefaultModel ?? "";
```

Remove the `Models.Clear()` block and model preference selection.

**`NewConversationAsync`:**

```csharp
var created = await _ai.CreateConversationAsync(serverUrl, token, null);
// ... ActiveConversationModel = created.Model ...
```

**`SelectConversationAsync`:** set `ActiveConversationModel = detail?.Model ?? ""`.

**`SendMessageAsync`:** inside the streaming loop, handle status:

```csharp
await foreach (var chunk in _ai.SendMessageStreamingAsync(serverUrl, token, conversationId, message, ct))
{
    if (string.Equals(chunk.Status, "queued", StringComparison.OrdinalIgnoreCase))
    {
        Dispatch(() =>
        {
            IsQueued = true;
            if (chunk.Position is int p) QueuePosition = p;
            if (chunk.Total is int t) QueueTotal = t;
        });
        continue;
    }

    if (chunk.Content is { Length: > 0 })
    {
        accumulated.Append(chunk.Content);
        if (IsModelLoading)
            Dispatch(() => IsModelLoading = false);
    }

    Dispatch(() =>
    {
        IsQueued = false;
        StreamingContent = accumulated.ToString();
        ScrollRequested?.Invoke();
    });

    if (chunk.Done)
        break;
}
```

- Do **not** start `StartModelLoadTimer()` until the first non-queued chunk arrives.
- Clear `IsQueued`/`QueuePosition`/`QueueTotal` in the `finally`.

### 11.4 Page XAML

File: `src/Clients/DotNetCloud.Client.Android/Views/AiPage.xaml`

1. **Conversation-list model picker** (`<Picker ...>` in `Grid.Row="1"`) → replace with a
   static label:

```xml
<Grid Grid.Row="1" ColumnDefinitions="Auto,*" Padding="16,4">
    <Label Text="Model:" TextColor="#94A3B8" VerticalOptions="Center"/>
    <Label Grid.Column="1"
           Text="{Binding DefaultModel}"
           TextColor="#F1F5F9"
           VerticalOptions="Center"/>
</Grid>
```

2. **Chat header model line** — change `Text="{Binding SelectedModel}"` to
   `Text="{Binding ActiveConversationModel}"`.

3. **Queue status** — inside the streaming bubble (`VerticalStackLayout Grid.Row="3"`),
   before the model-loading grid, add:

```xml
<Border IsVisible="{Binding IsQueued}"
        BackgroundColor="#1E293B"
        StrokeThickness="0"
        StrokeShape="RoundRectangle 10"
        Padding="12,6"
        Margin="0,0,0,4">
    <Label Text="{Binding QueueStatusText}"
           TextColor="#FDBA74"
           FontSize="12"/>
</Border>
```

4. Send button already disabled by `IsStreaming` (queued counts as streaming, since
   `IsStreaming` stays true during the queue wait) — no change.

---

## 12. Part 9 — Tests

### 12.1 `AiChatServiceTests`

File: `tests/DotNetCloud.Modules.AI.Tests/AiChatServiceTests.cs`

- Update the constructor call: add `new AiCompletionQueue()` (or a mock `IAiCompletionQueue`)
  and a `Mock<IAiSettingsProvider>` returning a fixed `DefaultModel`.
- Update all `CreateConversationAsync` calls to the new 3-arg signature and assert the
  persisted `Model` equals the mocked default (proves the client cannot choose).
- Keep existing `SendMessage`/`ListModels` tests; the queue serializes but tests are
  single-threaded so behavior is unchanged.

### 12.2 New `AiCompletionQueueTests`

File: `tests/DotNetCloud.Modules.AI.Tests/AiCompletionQueueTests.cs` (new)

Cover at minimum:

- `EnqueueStreaming_ProcessesWorkSequentially`: two work items; assert the second does not
  start until the first completes (use `TaskCompletionSource` gates inside the work delegates).
- `EnqueueStreaming_ReportsPositionAndTotal`: enqueue three; assert entry positions are 1,2,3
  and total is 3; complete the first; assert remaining positions shift to 1,2 and total 2.
- `EnqueueStreaming_CancelledWhileQueued_IsRemoved`: cancel entry 2's token; assert entry 3
  becomes position 2 and the cancelled work never runs.
- `EnqueueTaskAsync_ReturnsResult`.

### 12.3 `AiViewModelTests`

File: `tests/DotNetCloud.Client.Android.Tests/ViewModels/AiViewModelTests.cs`

- Update `CreateConversationAsync` mock setups (no model arg).
- Add `GetSettingsAsync` mock setup returning `AiSettingsDto { DefaultModel = "gpt-oss:20b" }`.
- Update `LoadAsync` assertions (assert `DefaultModel` populated; remove `Models` assertions).
- Add a test: a queued chunk (status `queued`, position 3, total 8) sets `IsQueued=true`,
  `QueuePosition=3`, `QueueTotal=8`, and `QueueStatusText` equals "In queue: position 3 of 8".

### 12.4 Other test impact

- `tests/DotNetCloud.Modules.AI.Tests/OllamaClientTests.cs` — unchanged.
- Any test or integration factory that references `AddAiUiServices`/in-process AI — update to
  register a stub `IAiApiClient` instead (mirror the existing `DotNetCloudWebApplicationFactory`
  chat stub pattern at `tests/DotNetCloud.Integration.Tests/Infrastructure/DotNetCloudWebApplicationFactory.cs`).

---

## 13. Part 10 — Build & verification

```bash
dotnet build DotNetCloud.CI.slnf -c Release
dotnet test tests/DotNetCloud.Modules.AI.Tests/
dotnet test tests/DotNetCloud.Client.Android.Tests/
```

Manual verification:

1. Two browser sessions submit messages at the same time → the second shows
   "In queue: position 2 of 2", then streams; Ollama receives exactly one request at a time.
2. Android emulator: same behavior through `/api/v1/ai/conversations/{id}/messages/stream`;
   the model picker is gone and the default model shows as static text.
3. Confirm a newly created conversation persists the admin `DefaultModel` in its `Model` column.
4. Cancel a queued request (close the tab while waiting) → the next request advances.

---

## 14. Notes / risks

- **Reverse-proxy timeouts:** SSE and gRPC streams stay open during long queue waits. If the
  deployment uses nginx with a short `proxy_read_timeout`, raise it for `/api/v1/ai/`.
- **`ListModels`** becomes unused by clients. Keep the RPC/REST endpoint for now; remove later
  if desired.
- **`AiAdminSettings.razor`** (admin UI) is unchanged — the `DefaultModel` setting already
  exists in `dbo.SystemSettings` and is read via `IAiSettingsProvider.GetDefaultModelAsync()`.
- **User identity:** the gRPC path previously used `CallerContext.CreateSystemContext()`; this
  plan fixes it by parsing `user_id` into a real `CallerContext`. Without this, Blazor
  conversations would be shared across users.
- **Queue is per-process (module host).** Blazor must go through the module host gRPC client —
  which this plan enforces — otherwise the two processes could still hit Ollama concurrently.

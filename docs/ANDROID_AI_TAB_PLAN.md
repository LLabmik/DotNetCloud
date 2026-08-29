# Android AI Tab — Implementation Plan

> Branch: `feature/android-ai-tab`
> Status: Ready for implementation
> Audience: implementation agent (lesser LLM). This document is self-contained — follow it literally.

## 0. Goal

Add an **AI Assistant** tab to the Android (MAUI) client that mirrors the Blazor AI chat page.
The tab is **hidden by default** and only shown when the optional **`dotnetcloud.ai`** module is
installed and reachable on the connected server — exactly like the Music tab.

Features (v1 scope):

- Conversation list + new chat
- Model selector (from `GET /api/v1/ai/models`)
- Send a message and **stream** the assistant reply (Server-Sent Events)
- Delete conversation
- Rename conversation
- Ollama health warning banner
- Lightweight Markdown rendering of assistant replies (no external NuGet)

Out of scope (v1): AI admin/provider settings, offline queue integration for AI, gRPC client changes.

The work is split into two independent tracks:

- **Phase A — server changes** (proxy route, AI host auth, rename endpoint). This is a **handoff**
  to the server agent on `cloud.kimball.home`. Do **not** implement it on the Android machine.
- **Phases B–F — Android client changes**. These are implemented locally and can be built/tested
  independently (E2E chat requires Phase A to be deployed).

---

## 1. Architecture summary (read this first)

- Module identifiers stored in `InstalledModules.ModuleId` are **full ids**: `dotnetcloud.music`,
  `dotnetcloud.ai`, etc. (source of truth: `src/Core/DotNetCloud.Core.Server/Initialization/ModuleUiRegistrationHostedService.cs`).
- Core.Server exposes `GET /api/v1/core/modules/{moduleId}/available` (any authenticated user) that
  returns `{ "success": true, "data": { "installed": <bool> } }`. Controller:
  `src/Core/DotNetCloud.Core.Server/Controllers/ModulesController.cs`.
- Core.Server reverse-proxies module REST APIs to the running module host process. The mapping lives in
  `MapModuleApiProxies` in `src/Core/DotNetCloud.Core.Server/Program.cs` (e.g. `["api/v1/music"] = "dotnetcloud.music"`).
  The proxy forwards the request **path unchanged**, so the proxy prefix must equal the module host's
  controller route.
- Core.Server's `ResponseEnvelopeMiddleware` **buffers the entire response into a MemoryStream** and wraps
  JSON in `{ success, data }`. Streaming endpoints must be excluded via `UseResponseEnvelope` → `ExcludePaths`.
- Module hosts authenticate Bearer tokens by calling Core.Server's token-introspection gRPC service, using a
  `DotNetCloud.Module` policy scheme. Canonical example: `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs`.
- Android detects optional modules in `App.xaml.cs` → `CheckAvailableModulesAsync()` (run at startup and after
  login), stores results in the static `ModuleAvailabilityState`, and shows/hides tab `ShellContent` entries via
  `AppShell.SetXxxTabVisible`.

---

## 2. AI REST API contract (what the Android client talks to)

After Phase A, the AI module REST API is reachable through the public origin at `/api/v1/ai`.

| Method | Path                                            | Body                                                              | Success response                                                 |
| ------ | ----------------------------------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------------- |
| GET    | `/api/v1/ai/models`                             | —                                                                 | JSON array of `LlmModelInfo` (see below)                         |
| GET    | `/api/v1/ai/conversations`                      | —                                                                 | JSON array of `ConversationDto`                                  |
| GET    | `/api/v1/ai/conversations/{id}`                 | —                                                                 | `ConversationDto` (with `messages`)                              |
| POST   | `/api/v1/ai/conversations`                      | `{ "title": string?, "model": string?, "systemPrompt": string? }` | `ConversationDto`                                                |
| DELETE | `/api/v1/ai/conversations/{id}`                 | —                                                                 | `204 No Content` (or `404`)                                      |
| PATCH  | `/api/v1/ai/conversations/{id}/title`           | `{ "title": string }`                                             | `{ "success": true }` (or `404`)                                 |
| POST   | `/api/v1/ai/conversations/{id}/messages`        | `{ "message": string }`                                           | `ChatResponseDto` (non-streaming)                                |
| POST   | `/api/v1/ai/conversations/{id}/messages/stream` | `{ "message": string }`                                           | SSE stream (below)                                               |
| GET    | `/api/v1/ai/health/ollama`                      | —                                                                 | `200 { "status": "healthy" }` or `503 { "status": "unhealthy" }` |

All JSON property names are **camelCase** (ASP.NET Core default). The Android client must deserialize
case-insensitively.

### 2.1 DTO shapes

`LlmModelInfo` (from `src/Core/DotNetCloud.Core/AI/LlmModelInfo.cs`, serialized camelCase):

```json
{
  "id": "gpt-oss:20b",
  "name": "gpt-oss:20b",
  "provider": "ollama",
  "sizeBytes": 12884901888,
  "parameterSize": "20B",
  "modifiedAt": "2026-08-01T00:00:00Z"
}
```

`ConversationDto` (from `AiChatController`):

```json
{
  "id": "…guid…",
  "title": "New Chat",
  "model": "gpt-oss:20b",
  "systemPrompt": null,
  "createdAt": "…",
  "updatedAt": "…",
  "messages": [
    {
      "id": "…",
      "role": "user|assistant|system",
      "content": "…",
      "createdAt": "…"
    }
  ]
}
```

`messages` is present only on `GET /conversations/{id}` (detail). The list endpoint returns summaries without
`messages`.

`ChatResponseDto` (non-streaming): `{ "model": "…", "content": "…", "done": true, "promptEvalCount": n, "evalCount": n }`.

### 2.2 SSE stream format (`/messages/stream`)

Content-Type `text/event-stream`. Each chunk is a single line:

```
data: {"content":"…","done":false,"evalCount":null}

```

Repeated; the final chunk is `done:true` and may carry `evalCount`. The stream ends with:

```
data: [DONE]

```

`content` is JSON-escaped (newlines are `\n` escapes), so each `data:` payload is always one physical line.
The Android client reads line-by-line: skip lines not starting with `data:`, strip the `data: ` prefix, stop on
`[DONE]`, otherwise deserialize `{ content, done, evalCount }`.

---

## 3. Phase A — Server changes (HANDOFF to cloud.kimball.home)

Write the following into `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md` → **Active Handoff** with target
`cloud.kimball.home`. Do **not** make these edits on the Android machine. All four changes must land together.

### A1. Add AI proxy route — `src/Core/DotNetCloud.Core.Server/Program.cs`

In `MapModuleApiProxies`, inside the `moduleMappings` dictionary, add (alphabetically, e.g. right after the
`api/v1/files` entry or grouped with the others):

```csharp
["api/v1/ai"] = "dotnetcloud.ai",
```

No other change to the proxy loop is needed — it already forwards the path unchanged and targets the module's
`GrpcEndpoint`.

### A2. Exclude AI from response enveloping — `src/Core/DotNetCloud.Core.Server/Program.cs`

In the `app.UseResponseEnvelope(...)` call (~line 847), add to `options.ExcludePaths`:

```csharp
"/api/v1/ai/",
```

This is **required** so the SSE stream is not buffered/wrapped (same reason `/api/v1/music/` is already excluded).

### A3. Fix AI host auth + route + caller context — `src/Modules/AI/DotNetCloud.Modules.AI.Host/`

**A3a — route + authorize:** In `Controllers/AiChatController.cs`:

- Change `[Route("api/ai")]` → `[Route("api/v1/ai")]`.
- Add `using Microsoft.AspNetCore.Authorization;` and add `[Authorize]` on the class (directly above `[ApiController]`
  or above the class declaration).
- Replace `GetCallerContext()` so it throws when unauthenticated instead of falling back to a system context
  (mirror `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/MusicControllerBase.cs` →
  `GetAuthenticatedCaller()`):

```csharp
private CallerContext GetCallerContext()
{
    if (User?.Identity?.IsAuthenticated != true)
        throw new UnauthorizedAccessException("Authentication is required.");

    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
        throw new UnauthorizedAccessException("Authenticated user identifier is invalid.");

    var roles = User.FindAll(System.Security.Claims.ClaimTypes.Role)
        .Select(c => c.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return new CallerContext(userId, roles, CallerType.User);
}
```

**A3b — add rename endpoint:** In `Controllers/AiChatController.cs`, add (uses the existing
`IAiChatService.RenameConversationAsync`):

```csharp
/// <summary>Renames a conversation.</summary>
[HttpPatch("conversations/{conversationId:guid}/title")]
public async Task<IActionResult> RenameConversation(
    Guid conversationId,
    [FromBody] RenameConversationRequest request,
    CancellationToken cancellationToken)
{
    var caller = GetCallerContext();
    var renamed = await _chatService.RenameConversationAsync(
        caller, conversationId, request.Title, cancellationToken);

    return renamed ? Ok(new { success = true }) : NotFound();
}
```

And add the request type (next to the other request DTOs in the same file):

```csharp
/// <summary>Request to rename a conversation.</summary>
public sealed class RenameConversationRequest
{
    /// <summary>The new title.</summary>
    public required string Title { get; set; }
}
```

**A3c — host auth:** In `Program.cs`, port the Chat host auth. Replace the current cookie-only block
(`builder.Services.AddAuthentication("Identity.Application").AddCookie(...)` + `builder.Services.AddAuthorization();`)
with the policy-scheme version from `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs`:

Add usings:

```csharp
using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Auth.Introspection;
```

Replace the auth registration with:

```csharp
// Register token introspection client (replaces local JWT key validation).
builder.Services.AddTokenIntrospection();

// Authentication: supports both cookie (browser/Blazor) and introspection (desktop/mobile).
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "DotNetCloud.Module";
        options.DefaultAuthenticateScheme = "DotNetCloud.Module";
        options.DefaultChallengeScheme = "DotNetCloud.Module";
    })
    .AddCookie("Identity.Application", options =>
    {
        // (keep the existing cookie options exactly as they are today)
    })
    .AddIntrospection(IntrospectionAuthenticationExtensions.SchemeName)
    .AddPolicyScheme("DotNetCloud.Module", "DotNetCloud.Module", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var auth)
                && auth.Count > 0
                && auth[0]?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return IntrospectionAuthenticationExtensions.SchemeName;
            }
            return "Identity.Application";
        };
    });

builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options));
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

Keep `app.UseAuthentication(); app.UseAuthorization();` in the pipeline (already present).

**Server verify (cloud.kimball.home):** with the AI module installed and Ollama reachable, confirm
`GET /api/v1/ai/models` returns 200 with a valid Bearer token and 401 without, and a create → send → list
round-trip is per-user.

---

## 4. Phase B — Android module availability + Music short-id fix

### B1. `src/Clients/DotNetCloud.Client.Android/Services/ModuleAvailabilityState.cs`

Add the AI accessors next to the existing Music ones (the class already has `_availableModules`,
`SetModuleAvailable`, `IsModuleAvailable`, `ClearAll`):

```csharp
/// <summary>Whether the AI module is installed and available on the connected server.</summary>
public static bool IsAiModuleAvailable => _availableModules.Contains("AI");

/// <summary>Fired when <see cref="IsAiModuleAvailable"/> changes.</summary>
public static event Action? AiAvailabilityChanged;

/// <summary>Sets <see cref="IsAiModuleAvailable"/> and fires <see cref="AiAvailabilityChanged"/>.</summary>
public static void SetAiAvailable(bool available)
{
    SetModuleAvailable("AI", available);
    AiAvailabilityChanged?.Invoke();
}
```

### B2. `src/Clients/DotNetCloud.Client.Android/AppShell.xaml`

Inside the `<TabBar Route="Main">`, add a hidden AI tab (e.g. after the Notes tab, before Settings):

```xml
<ShellContent
    x:Name="AiTab"
    Route="Ai"
    Title="AI"
    Icon="ai_icon.png"
    IsVisible="False"
    ContentTemplate="{DataTemplate views:AiPage}"/>
```

### B3. `src/Clients/DotNetCloud.Client.Android/AppShell.xaml.cs`

Add `private static ShellContent? _aiTab;` next to `_musicTab`. In the constructor, after `_musicTab = MusicTab;`,
add `_aiTab = AiTab;`. Then:

```csharp
/// <summary>Shows/hides the AI tab.</summary>
public static void SetAiTabVisible(bool visible)
{
    if (_aiTab is not null)
        _aiTab.IsVisible = visible;
}

/// <summary>
/// Re-reads <see cref="ModuleAvailabilityState"/> and updates all tab visibilities
/// accordingly. Called after a full module rescan.
/// </summary>
public static void RefreshAllTabs()
{
    SetMusicTabVisible(ModuleAvailabilityState.IsMusicModuleAvailable);
    SetAiTabVisible(ModuleAvailabilityState.IsAiModuleAvailable);
}
```

### B4. `src/Clients/DotNetCloud.Client.Android/App.xaml.cs` — probe AI + fix Music id

**B4a — generalize the availability endpoint helper.** Rename/replace the existing
`CheckMusicModuleEndpointAsync(string baseUrl, string token)` with a generic
`CheckModuleEndpointAsync(string baseUrl, string token, string moduleId)` that builds
`$"{baseUrl}/api/v1/core/modules/{moduleId}/available"`. Its body is otherwise unchanged (parse
`data.installed` as bool, catch → false).

**B4b — in `CheckAvailableModulesAsync()`**, replace the music availability call with the full id, and add an
AI check after the music block. The relevant section becomes:

```csharp
// ── Check Music module ──────────────────────────────────
var musicAvailable = await CheckModuleEndpointAsync(baseUrl, token, "dotnetcloud.music");
if (!musicAvailable)
    musicAvailable = await ProbeMusicApiAsync(baseUrl, token);

ModuleAvailabilityState.SetMusicAvailable(musicAvailable);
if (musicAvailable)
    AppShell.SetMusicTabVisible(true);

// ── Check AI module ─────────────────────────────────────
var aiAvailable = await CheckModuleEndpointAsync(baseUrl, token, "dotnetcloud.ai");
if (!aiAvailable)
    aiAvailable = await ProbeAiApiAsync(baseUrl, token);

ModuleAvailabilityState.SetAiAvailable(aiAvailable);
if (aiAvailable)
    AppShell.SetAiTabVisible(true);
```

Add the AI probe method near `ProbeMusicApiAsync`:

```csharp
private static async Task<bool> ProbeAiApiAsync(string baseUrl, string token)
{
    try
    {
        var url = $"{baseUrl}/api/v1/ai/models";
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await http.GetAsync(url);
        var status = (int)response.StatusCode;
        // 200 or 401 means the endpoint exists (module reachable through the proxy).
        // 404 means not proxied / not installed.
        return status == 200 || status == 401;
    }
    catch
    {
        return false;
    }
}
```

Also update the `catch` block of `CheckAvailableModulesAsync()` to reset AI too:

```csharp
ModuleAvailabilityState.SetMusicAvailable(false);
AppShell.SetMusicTabVisible(false);
ModuleAvailabilityState.SetAiAvailable(false);
AppShell.SetAiTabVisible(false);
```

> Note: `ProbeAiApiAsync` only returns true after Phase A (proxy route) is deployed. The availability
> endpoint check (`dotnetcloud.ai`) works regardless, so the tab appears once the module is installed in the
> DB even before the proxy exists; the in-page data load will then surface an error until Phase A lands.

---

## 5. Phase C — Android AI REST client + DTOs + SSE parser

Create a new folder `src/Clients/DotNetCloud.Client.Android/Ai/`.

### C1. `Ai/IAiRestClient.cs`

Mirror `Music/IMusicRestClient.cs` (per-call `serverBaseUrl` + `accessToken`):

```csharp
namespace DotNetCloud.Client.Android.Ai;

/// <summary>REST API client for the AI module (base path /api/v1/ai).</summary>
public interface IAiRestClient
{
    Task<IReadOnlyList<AiModelDto>> ListModelsAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<AiConversationDto>> ListConversationsAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);
    Task<AiConversationDto?> GetConversationAsync(string serverBaseUrl, string accessToken, Guid conversationId, CancellationToken ct = default);
    Task<AiConversationDto?> CreateConversationAsync(string serverBaseUrl, string accessToken, string? title, string model, CancellationToken ct = default);
    Task<bool> DeleteConversationAsync(string serverBaseUrl, string accessToken, Guid conversationId, CancellationToken ct = default);
    Task<bool> RenameConversationAsync(string serverBaseUrl, string accessToken, Guid conversationId, string newTitle, CancellationToken ct = default);
    IAsyncEnumerable<AiStreamChunk> SendMessageStreamingAsync(string serverBaseUrl, string accessToken, Guid conversationId, string message, CancellationToken ct = default);
    Task<bool> GetOllamaHealthAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default);
}
```

### C2. `Ai/AiDtos.cs`

```csharp
namespace DotNetCloud.Client.Android.Ai;

public sealed record AiConversationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Model { get; init; } = "";
    public string? SystemPrompt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<AiMessageDto>? Messages { get; init; }
}

public sealed record AiMessageDto
{
    public Guid Id { get; init; }
    public string Role { get; init; } = "";
    public string Content { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

public sealed record AiModelDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Provider { get; init; } = "";
    public long? SizeBytes { get; init; }
    public string? ParameterSize { get; init; }
    public DateTime? ModifiedAt { get; init; }
}

public sealed record AiStreamChunk
{
    public string Content { get; init; } = "";
    public bool Done { get; init; }
    public int? EvalCount { get; init; }
}
```

### C3. `Ai/HttpAiRestClient.cs`

Copy the structure of `Music/HttpMusicRestClient.cs`:

- `private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };`
- `SetAuth(string accessToken)` sets `Authorization: Bearer …` on `DefaultRequestHeaders`.
- `BaseUrl(string serverBaseUrl) => serverBaseUrl.TrimEnd('/');`
- `ReadEnvelopeDataAsync<T>` helper: parse the body; if root is an object with a `data` property, deserialize
  that, otherwise deserialize the root itself (defensive — same as music).
- Register the type with the same HttpClient handler chain as music (Phase F).

Implement:

```csharp
public async Task<IReadOnlyList<AiModelDto>> ListModelsAsync(string serverBaseUrl, string accessToken, CancellationToken ct = default)
{
    SetAuth(accessToken);
    var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/models";
    using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    return JsonSerializer.Deserialize<List<AiModelDto>>(body, JsonOpts) ?? [];
}
```

`ListConversationsAsync`, `GetConversationAsync`, `CreateConversationAsync` (POST `{ title, model }`), and
`RenameConversationAsync` (PATCH `…/title` with `{ title }`) follow the same GET/POST envelope-aware pattern.

`DeleteConversationAsync` returns true on 204, false on 404 (do not throw on 404):

```csharp
SetAuth(accessToken);
using var response = await _http.DeleteAsync($"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations/{conversationId}", ct)
    .ConfigureAwait(false);
if (response.StatusCode == HttpStatusCode.NotFound) return false;
response.EnsureSuccessStatusCode();
return true;
```

`GetOllamaHealthAsync` returns `response.IsSuccessStatusCode` (GET `/api/v1/ai/health/ollama`).

`SendMessageStreamingAsync` (SSE):

```csharp
public async IAsyncEnumerable<AiStreamChunk> SendMessageStreamingAsync(
    string serverBaseUrl, string accessToken, Guid conversationId, string message,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
{
    SetAuth(accessToken);
    var url = $"{BaseUrl(serverBaseUrl)}/api/v1/ai/conversations/{conversationId}/messages/stream";
    var json = JsonSerializer.Serialize(new { message }, JsonOpts);
    using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

    using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
        .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    using var reader = new StreamReader(stream);
    while (!ct.IsCancellationRequested)
    {
        var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        if (line is null)
            break;
        if (!line.StartsWith("data:", StringComparison.Ordinal))
            continue;
        var payload = line["data:".Length..].Trim();
        if (payload == "[DONE]")
            yield break;
        var chunk = JsonSerializer.Deserialize<AiStreamChunk>(payload, JsonOpts);
        if (chunk is not null)
            yield return chunk;
    }
}
```

---

## 6. Phase D — `src/Clients/DotNetCloud.Client.Android/ViewModels/AiViewModel.cs`

Model after `ViewModels/MusicViewModel.cs` (CommunityToolkit `[ObservableProperty]`, `[RelayCommand]`).
Dependencies: `IAiRestClient`, `IServerConnectionStore`, `ISecureTokenStore`, and `ITokenRefreshService`
(preferred for fresh tokens). Include the same `Dispatch(Action)` main-thread wrapper that swallows
`NotImplementedException` (copy it from `MusicViewModel`).

State:

- `ObservableCollection<AiConversationDto> Conversations`
- `ObservableCollection<AiMessageDto> ActiveMessages`
- `IReadOnlyList<AiModelDto> Models` (or `ObservableCollection<AiModelDto>`)
- `string SelectedModel` (the `AiModelDto.Id` in use)
- `Guid? ActiveConversationId`
- `string ComposerText`
- `string StreamingContent`
- `bool IsStreaming`, `bool IsLoading`, `bool OllamaHealthy`, `bool ShowConversationList`
- `string? ErrorMessage`
- `bool IsRenameMode`, `string RenameTitle`

Behaviors:

- `LoadAsync()` — on first appear, resolve credentials; run `ListModelsAsync`, `ListConversationsAsync`,
  `GetOllamaHealthAsync` in parallel; set `SelectedModel` to `gpt-oss:20b` if present else `Models[0]`;
  default `ShowConversationList = true`.
- `NewConversationAsync()` — create with `SelectedModel`, insert at top of `Conversations`, clear
  `ActiveMessages`, set `ShowConversationList = false`, focus composer.
- `SelectConversationAsync(AiConversationDto)` — `GetConversationAsync(id)`, populate `ActiveMessages`,
  `ShowConversationList = false`.
- `SendMessageAsync()` — append the user message locally (`role = "user"`), clear `ComposerText`, set
  `IsStreaming = true`, `StreamingContent = ""`; iterate `SendMessageStreamingAsync(...)`, accumulating chunks
  into `StreamingContent` via `Dispatch`; on `[DONE]` (or stream end) append an assistant message
  (`role = "assistant"`, content = accumulated text) and refresh the conversation list (updated timestamps).
  Guard with a `CancellationTokenSource` so a new message cancels a running stream.
- `DeleteConversationAsync(AiConversationDto)` — call client; on true remove from `Conversations` and, if it was
  active, clear `ActiveMessages`/`ActiveConversationId`.
- `BeginRename(AiConversationDto)` / `CommitRenameAsync()` — call `RenameConversationAsync`, update the item title.
- `BackToList()` — `ShowConversationList = true`.

Markdown rendering is done in the view (Phase E), not the ViewModel; keep raw `Content` strings in the messages.

---

## 7. Phase E — Android AI UI

### E1. `src/Clients/DotNetCloud.Client.Android/Views/AiPage.xaml` + `AiPage.xaml.cs`

Constructor-inject `AiViewModel` (like `MusicPage`). `Shell.NavBarIsVisible="true"`, dark palette
(`#1E293B` nav, `#0F172A` background) matching `MusicPage`.

Use a top-level `Grid` with two children toggled by `IsVisible`:

1. **Conversation list** (`IsVisible="{Binding ShowConversationList}"`):
   - Header row: title "AI Assistant", a "New chat" button (`Command="{Binding NewConversationCommand}"`).
   - A `Picker` bound to `Models` (ItemDisplayBinding `{Binding Name}`), `SelectedItem` → `SelectedModel`.
   - A `CollectionView` bound to `Conversations`. Each item is a `SwipeView` whose content is a `Grid` showing
     `Title` (or "Untitled") + `UpdatedAt` (short date), with right-side `SwipeItems`:
     - **Rename** (invokes `BeginRename` + opens an inline editor/`DisplayPromptAsync`)
     - **Delete** (invokes `DeleteConversationCommand`)
   - `TapGestureRecognizer` on the content → `SelectConversationCommand`.

2. **Chat view** (`IsVisible="{Binding ShowConversationList, Converter=InvertedBool}"` — add a tiny
   `InvertedBoolConverter` if not present, or bind a second computed property `IsChatVisible`):
   - Header: back button (`BackToListCommand`), conversation title, model label.
   - `CollectionView` bound to `ActiveMessages`, with a `DataTemplateSelector` (or triggers) for user vs assistant
     bubbles. Assistant content uses a `Label` with `FormattedText="{Binding Content, Converter={StaticResource Markdown}}"`.
   - Streaming bubble: visible while `IsStreaming`, shows `StreamingContent` (formatted) + a cursor `▍`.
   - Ollama warning banner: visible when `!OllamaHealthy` ("AI may be unavailable — Ollama is not reachable.").
   - Composer: `Editor` bound to `ComposerText` + a Send `Button` (`SendMessageCommand`), disabled while `IsStreaming`.
   - Error label bound to `ErrorMessage`.

Keep `CollectionView` `ItemsUpdatingScrollMode="KeepLastItemInView"` and, on new message/stream chunk, scroll to
bottom via `MessagesList.ScrollTo(Count - 1, position: End, animate: false)` from the page's code-behind (mirror the
`MessageListPage` near-bottom pattern only if desired; a simple always-scroll is acceptable for v1).

### E2. Lightweight Markdown formatter — `src/Clients/DotNetCloud.Client.Android/Converters/MarkdownConverter.cs`

Implement an `IValueConverter` (or a static helper + converter) `string` → `FormattedString` that handles, in
order:

1. Fenced code blocks ` `lang … ` ` → one `Span` per block with `FontFamily="monospace"` and a slightly
   darker background (approximate with `BackgroundColor` on the span; if span background is unreliable, just use
   monospace + a leading/trailing blank line).
2. Inline `` `code` `` → monospace span.
3. `**bold**` → bold span; `*italic*` → italic span.
4. `[text](url)` → span with the text, colored `#0EA5E9` and underlined; optionally attach a
   `TapGestureRecognizer` on the `Label` that opens the URL via `Launcher.Default.OpenAsync`.

A simple scanner (character loop) is sufficient. Keep it deterministic and unit-testable (pure function in,
`FormattedString` out). No external NuGet package.

---

## 8. Phase F — DI, icon, tests

### F1. `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`

Add near the Music section (~line 94):

```csharp
builder.Services.AddHttpClient<IAiRestClient, HttpAiRestClient>()
    .AddHttpMessageHandler<TimeoutHandler>()
    .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .ConfigurePrimaryHttpMessageHandler(DotNetCloud.Client.Core.Auth.OAuthHttpClientHandlerFactory.CreateHandler);
```

In the ViewModels block add `builder.Services.AddTransient<AiViewModel>();`.
In the pages block add `builder.Services.AddTransient<AiPage>();`.

### F2. Icon

Add `src/Clients/DotNetCloud.Client.Android/Resources/Images/ai_icon.svg` (copy `music_icon.svg` and replace the
path data with a simple robot/sparkle glyph; `<MauiImage Include="Resources\Images\*" />` already globs it).
Referenced as `ai_icon.png` in AppShell.

### F3. Tests

- `tests/DotNetCloud.Client.Android.Tests/Services/ModuleAvailabilityStateTests.cs` — add cases:
  `IsAiModuleAvailable_DefaultsToFalse`, `_CanBeSetToTrue`, `_CanBeToggledBackToFalse`,
  `SetAiAvailable_FiresEvent_WhenChanged`. Also add a case asserting `SetMusicAvailable` uses full-id semantics via
  `IsModuleAvailable` (i.e. the availability key is `"AI"`).
- Add `tests/DotNetCloud.Client.Android.Tests/Converters/MarkdownConverterTests.cs` for the formatter.
- Optionally add `AiViewModelTests` with a fake `IAiRestClient`.

> ⚠️ `tests/DotNetCloud.Client.Android.Tests/DotNetCloud.Client.Android.Tests.csproj` is currently **broken on a
> clean base** (missing `<Compile Include>` entries for the offline-queue types) and is excluded from
> `DotNetCloud.CI.slnf`. When adding new files under `src/Clients/DotNetCloud.Client.Android/`, add matching
> `<Compile Include>` entries (or a glob) to that test csproj so the test project compiles again. Do **not**
> delete any `.cs` files.

---

## 9. Build & verify

### Build

```powershell
dotnet build src\Clients\DotNetCloud.Client.Android -f net10.0-android -c Debug -r android-arm64 /p:AndroidSdkDirectory="C:\Program Files (x86)\Android\android-sdk"
dotnet test tests\DotNetCloud.Client.Android.Tests
```

> Always build with `-r android-arm64` — a bare `dotnet build` only builds x64 and leaves the arm64 APK stale.

### Manual/E2E (gated — required before commit)

Server prerequisite: Phase A deployed to `cloud.kimball.home` and the AI module installed + Ollama reachable.
Confirm:

1. `GET /api/v1/ai/models` returns 200 with a Bearer token; 401 without.
2. On device: AI tab hidden when the module is absent; visible after install + Settings → "Re-check which server
   modules are available".
3. Create conversation, select model, send a message → streamed reply renders, Markdown formats, delete and rename
   work, Ollama banner appears when unhealthy.
4. Music tab still shows correctly with the `dotnetcloud.music` fix.

If the server handoff has not landed (no reachable AI module/Ollama), **STOP and report before committing**.

---

## 10. Gotchas

- `InstalledModule.ModuleId` values are full ids — never use the short name for availability checks.
- The proxy forwards paths unchanged: keep the AI controller route and proxy prefix identical (`api/v1/ai`).
- `ResponseEnvelopeMiddleware` buffers responses; the AI SSE stream must be excluded from enveloping.
- Android tab detection uses the availability endpoint first and a probe fallback; the probe only succeeds after
  the proxy route is deployed.
- `AiChatController` had no rename endpoint on the REST side (only gRPC) — Phase A adds `PATCH …/title`.
- Follow the `.csproj` / `NoWarn` / `TreatWarningsAsErrors` conventions in the Android project (no new suppressions
  except where the existing code already does, e.g. CA1416 SDK guards).

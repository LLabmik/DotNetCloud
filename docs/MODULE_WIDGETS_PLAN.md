# Module Home Widgets — Implementation Plan

**Branch:** `feature/module-widgets`
**Status:** Ready for implementation
**Audience:** This document is written to be detailed enough for an LLM agent with no prior context to implement the feature end-to-end.

---

## 1. Summary

Add a "widget" card to the Home page (`/`) for each first-party module. Each widget shows a small amount of recent/informative data for the signed-in user. The "Your Apps" section on the Home page is removed (modules are already reachable via the sidebar); it is replaced by a grid of widget cards. The top summary cards (Account / Available Apps / Admin-Security) remain.

Each widget lives in its **own new project** — `DotNetCloud.Modules.<Module>.Widget` — so widget code is never added to existing module projects.

**Widgets (12 total; About and Example have none):**

| Module    | Widget shows                                                            |
| --------- | ----------------------------------------------------------------------- |
| Files     | Storage quota bar + 5 most recently modified files                      |
| Video     | 5 newest videos                                                         |
| Music     | 5 newest albums                                                         |
| Tracks    | Upcoming work items with due dates (assigned to me / watched, not done) |
| Calendar  | Upcoming events (next 7 days)                                           |
| Contacts  | 5 most recently added contacts                                          |
| Chat      | 5 most recently active channels                                         |
| Photos    | 5 most recent photos                                                    |
| Notes     | 5 most recently edited notes                                            |
| Bookmarks | 5 most recent bookmarks                                                 |
| Email     | 5 most recent inbox threads (or "No account configured")                |
| AI        | Conversation count + last activity (no titles)                          |

---

## 2. Locked decisions

1. All 12 widgets above. **About** module = no widget. **Example** module = no widget. **Search** = core capability, no widget.
2. Keep Home's top summary cards; replace only the "Your Apps" section.
3. Always add a dedicated server-side query method for "recent"/widget data — **no client-side sorting** of generic list endpoints.
4. New **standalone** `WidgetUiRegistry` + a dedicated hosted service (do NOT extend `ModuleUiRegistry`).
5. Widget order = module nav sort order (Files 10, Chat 20, Contacts 30, Calendar 40, Notes 50, Tracks 60, Photos 70, Music 80, Video 90, AI 100, Bookmarks 110, Email 120).
6. Widgets load once on initial render (no polling). Each widget handles its own loading/empty/error state so one failing widget cannot break the page.
7. Per-user show/hide + drag-and-drop reorder is **DEFERRED to a follow-up**. Not in this change.

---

## 3. Data-access paths (IMPORTANT — two patterns)

Blazor components render inside the Core.Server process. Modules fall into two categories for data access. A widget MUST follow the same path its module already uses.

### A. In-process modules (widget injects module services directly)

These modules register their business services in Core.Server's DI via `AddXxxUiServices(...)` in `src/Core/DotNetCloud.Core.Server/Program.cs`. Their services require a `CallerContext` parameter.

| Module | Widget injects                  | Registered by         |
| ------ | ------------------------------- | --------------------- |
| Files  | `IFileService`, `IQuotaService` | `AddFilesUiServices`  |
| Video  | `IVideoService`                 | `AddVideoUiServices`  |
| Music  | `IMusicAlbumService`            | `AddMusicUiServices`  |
| Photos | `IPhotoService`                 | `AddPhotosUiServices` |
| Notes  | `INoteService`                  | `AddNotesUiServices`  |
| Chat   | `IChannelService`               | `AddChatServices`     |
| Tracks | `WorkItemService` (concrete)    | `AddTracksUiServices` |

For these widgets, the component builds a `CallerContext` from the auth state (pattern in section 6.3).

### B. Process-isolated modules (widget injects a gRPC `I*ApiClient`)

These modules are reached only via gRPC. The client interfaces live in `DotNetCloud.Core` and are already registered in Core.Server (`AddScoped<I*ApiClient, *GrpcApiClient>`).

| Module    | Widget injects        | Interface file                                                         |
| --------- | --------------------- | ---------------------------------------------------------------------- |
| Calendar  | `ICalendarApiClient`  | `src/Core/DotNetCloud.Core/Services/ModuleApis/ICalendarApiClient.cs`  |
| Contacts  | `IContactsApiClient`  | `src/Core/DotNetCloud.Core/Services/ModuleApis/IContactsApiClient.cs`  |
| Bookmarks | `IBookmarksApiClient` | `src/Core/DotNetCloud.Core/Services/ModuleApis/IBookmarksApiClient.cs` |
| Email     | `IEmailApiClient`     | `src/Core/DotNetCloud.Core/Services/ModuleApis/IEmailApiClient.cs`     |
| AI        | `IAiApiClient`        | `src/Core/DotNetCloud.Core/Services/ModuleApis/IAiApiClient.cs`        |

For these widgets, the component does **NOT** build a `CallerContext`. The gRPC clients resolve the current user from `IHttpContextAccessor` internally (see `GetUserId()` in `src/Core/DotNetCloud.Core.Server/Grpc/Clients/ContactsGrpcApiClient.cs`). The widget just calls the client method with a count.

New methods on these clients require the full chain in section 8 (service → proto → gRPC service → client interface → gRPC client).

---

## 4. Conventions to follow (enforced at build)

- `TreatWarningsAsErrors` and `Nullable` are on globally via `Directory.Build.props`. Every public member needs an XML `<summary>` doc comment or the build fails (CS1591).
- File-scoped namespaces. `ImplicitUsings` enabled.
- **Blazor icons MUST use the `MaterialIcon` component** (`DotNetCloud.UI.Shared.Components.DataDisplay.MaterialIcon`). No raw emoji in new widget UI. Use Material icon ligature names (`folder`, `movie`, `music_note`, `event`, `contacts`, `chat`, `photo`, `note`, `bookmark`, `mail`, `smart_toy`, `task_alt`, `widgets`).
- Do not reference any module `.Host` project from Core.Server or from widget projects.
- New projects use `Microsoft.NET.Sdk.Razor` and central package management (`Directory.Packages.props`), so `<PackageReference>` entries have **no Version attribute**.
- Widget components must not throw during render; wrap data loading in try/catch and render a small error/empty message on failure.

---

## 5. Deliverables overview (files to create/modify)

**New projects (12):**
`src/Modules/{Files,Video,Music,Photos,Notes,Chat,Tracks,Calendar,Contacts,Bookmarks,Email,AI}/DotNetCloud.Modules.{Module}.Widget/`

**New shared infrastructure (3 files):**

- `src/UI/DotNetCloud.UI.Web/Services/WidgetUiRegistry.cs`
- `src/UI/DotNetCloud.UI.Shared/Components/DataDisplay/WidgetCard.razor` (+ `.razor.css`)
- `src/Core/DotNetCloud.Core.Server/Initialization/WidgetUiRegistrationHostedService.cs`

**Modified existing files:**

- `src/UI/DotNetCloud.UI.Web/Components/Pages/Home.razor`
- `src/Core/DotNetCloud.Core.Server/Program.cs`
- `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`
- `DotNetCloud.sln`
- `DotNetCloud.CI.slnf`
- Service interfaces/implementations for Photos, Notes, Chat, Tracks (in-process methods)
- Service + proto + gRPC + client files for Calendar, Contacts, Bookmarks, Email, AI (process-isolated methods)
- `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` (tracking updates)

---

## 6. Phase 0 — Shared infrastructure

### 6.1 `WidgetUiRegistry` (new)

Create `src/UI/DotNetCloud.UI.Web/Services/WidgetUiRegistry.cs`.

Model it directly on `src/UI/DotNetCloud.UI.Web/Services/ModuleUiRegistry.cs`. Namespace `DotNetCloud.UI.Web.Services`.

Contents (signatures; keep it minimal and mirror `ModuleUiRegistry`):

```csharp
namespace DotNetCloud.UI.Web.Services;

/// <summary>
/// Manages widget component registration for the Blazor shell.
/// </summary>
public sealed class WidgetUiRegistry
{
    private readonly List<WidgetDescriptor> _widgets = [];

    public event Action? OnChange;

    public IReadOnlyList<WidgetDescriptor> Widgets => _widgets;

    public void RegisterWidget(string moduleId, string title, string icon, string href, Type componentType, int sortOrder = 100)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        _widgets.RemoveAll(w => w.ModuleId == moduleId);   // idempotent re-registration
        _widgets.Add(new WidgetDescriptor(moduleId, title, icon, href, componentType, sortOrder));
        _widgets.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        OnChange?.Invoke();
    }

    public void UnregisterModule(string moduleId)
    {
        _widgets.RemoveAll(w => w.ModuleId == moduleId);
        OnChange?.Invoke();
    }
}

/// <summary>Describes a widget registered by a module.</summary>
public sealed record WidgetDescriptor(
    string ModuleId,
    string Title,
    string Icon,
    string Href,
    Type ComponentType,
    int SortOrder);
```

### 6.2 `WidgetCard` shared shell (new)

Create `src/UI/DotNetCloud.UI.Shared/Components/DataDisplay/WidgetCard.razor` and `WidgetCard.razor.css`.

The card renders a header (MaterialIcon + title + optional "Open" link) and a body slot (`ChildContent`). It does NOT know anything about data — the widget component renders inside `ChildContent`.

`WidgetCard.razor`:

```razor
@namespace DotNetCloud.UI.Shared.Components.DataDisplay

<div class="widget-card">
    <div class="widget-card-header">
        <MaterialIcon Icon="@Icon" />
        <h3 class="widget-card-title">@Title</h3>
        @if (!string.IsNullOrWhiteSpace(Href))
        {
            <a class="widget-card-open" href="@Href">Open</a>
        }
    </div>
    <div class="widget-card-body">
        @ChildContent
    </div>
</div>

@code {
    [Parameter] public string Title { get; set; } = string.Empty;
    [Parameter] public string Icon { get; set; } = "widgets";
    [Parameter] public string Href { get; set; } = string.Empty;
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

Add simple CSS in `WidgetCard.razor.css` for `.widget-card`, `.widget-card-header`, `.widget-card-title`, `.widget-card-open`, `.widget-card-body`. Match the existing card styling used in `Home.razor` (`dashboard-card` / `module-card`). If `RenderFragment` is not in scope, add `@using Microsoft.AspNetCore.Components` at the top of the file.

### 6.3 CallerContext helper pattern (for in-process widgets)

Copy the pattern from `src/UI/DotNetCloud.UI.Web/Components/Shared/GlobalChatNotifications.razor.cs` (`GetCallerContextAsync`). Each in-process widget code-behind includes:

```csharp
private async Task<DotNetCloud.Core.Authorization.CallerContext> BuildCallerAsync()
{
    var state = await AuthStateProvider.GetAuthenticationStateAsync();
    var user = state.User;
    var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value;
    if (!Guid.TryParse(userIdClaim, out var userId))
        throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
    var roles = user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
    return new DotNetCloud.Core.Authorization.CallerContext(userId, roles, CallerType.User);
}
```

(`CallerType` is `DotNetCloud.Core.Authorization.CallerType`; `CallerContext` is `DotNetCloud.Core.Authorization.CallerContext`.)

### 6.4 `WidgetUiRegistrationHostedService` (new)

Create `src/Core/DotNetCloud.Core.Server/Initialization/WidgetUiRegistrationHostedService.cs`.

Model it on `src/Core/DotNetCloud.Core.Server/Initialization/ModuleUiRegistrationHostedService.cs` — BUT it must be read-only: it does **not** seed `InstalledModules` (that is `ModuleUiRegistrationHostedService`'s job; it seeds on first run at startup). Because this service polls every 15s, any initial empty read self-heals on the next tick.

Requirements:

- Namespace `DotNetCloud.Core.Server.Initialization`.
- `internal sealed class WidgetUiRegistrationHostedService : BackgroundService`.
- Constructor takes `IServiceScopeFactory`, `WidgetUiRegistry`, `ILogger<WidgetUiRegistrationHostedService>`.
- `ExecuteAsync` runs `RefreshAsync` once, then a `PeriodicTimer(TimeSpan.FromSeconds(15))` loop.
- Wrap each refresh in try/catch (catch `OperationCanceledException` when cancelled and rethrow; catch all others and log, then continue — mirror `ModuleUiRegistrationHostedService.RefreshModuleUiRegistrationAsync`).
- `RefreshAsync` core:
  1. Create a scope; get `CoreDbContext`.
  2. Read `InstalledModules` (`.AsNoTracking()`) into a `Dictionary<string,string>` of moduleId → Status (OrdinalIgnoreCase).
  3. For each entry in a `KnownWidgetDescriptors` array, if status == `"Enabled"`, call `_widgetUiRegistry.RegisterWidget(...)`; else `_widgetUiRegistry.UnregisterModule(...)`.
  4. Log a summary at Information level.

`KnownWidgetDescriptors` is a `static readonly` array of `(ModuleId, Title, Icon, Href, ComponentType, SortOrder)`. Component types are referenced with `typeof(...)` from the widget projects (section 9). Titles/icons/hrefs/sort orders are listed in section 1/2.

### 6.5 Register the registry + hosted service in `Program.cs`

In `src/Core/DotNetCloud.Core.Server/Program.cs`:

- Find `builder.Services.AddSingleton<ModuleUiRegistry>();` and add `builder.Services.AddSingleton<WidgetUiRegistry>();` next to it.
- Find `builder.Services.AddHostedService<ModuleUiRegistrationHostedService>();` (or wherever that hosted service is registered) and add `builder.Services.AddHostedService<WidgetUiRegistrationHostedService>();` next to it.

If `ModuleUiRegistrationHostedService` is registered via a different mechanism (e.g., `AddHostedService` or manual `services.AddSingleton<IHostedService>`), match whatever it uses.

### 6.6 Update `Home.razor`

Modify `src/UI/DotNetCloud.UI.Web/Components/Pages/Home.razor`:

1. Remove the entire `<section class="module-home-section"> ... </section>` ("Your Apps" block).
2. In its place add:

```razor
<section class="widgets-section">
    <h2>Your Widgets</h2>
    @if (WidgetRegistry.Widgets.Count == 0)
    {
        <p class="text-muted">No widgets are available yet.</p>
    }
    else
    {
        <div class="widget-grid">
            @foreach (var widget in WidgetRegistry.Widgets)
            {
                <WidgetCard Title="@widget.Title" Icon="@widget.Icon" Href="@widget.Href">
                    <DynamicComponent Type="@widget.ComponentType" />
                </WidgetCard>
            }
        </div>
    }
</section>
```

3. In the `@code` block, add:

```csharp
[Inject] private WidgetUiRegistry WidgetRegistry { get; set; } = default!;
```

4. Keep the existing `[Inject] private ModuleUiRegistry ModuleRegistry` (still used by the "Available Apps" count card).
5. Optional hardening: subscribe to `WidgetRegistry.OnChange` in `OnInitialized` (call `StateHasChanged`) and unsubscribe in `Dispose` (make the component `IDisposable`), so the grid re-renders if widgets are registered after the page loads. `DynamicComponent` and `WidgetCard` are already available via the `_Imports.razor` usings (`DotNetCloud.UI.Shared.Components.DataDisplay` is imported).

---

## 7. Widget project template (used by all 12)

Each widget project contains exactly 4 files:

- `<Module>Widget.razor`
- `<Module>Widget.razor.cs` (code-behind partial class)
- `_Imports.razor`
- `DotNetCloud.Modules.<Module>.Widget.csproj`

### 7.1 csproj — in-process modules

For Files, Video, Music, Photos, Notes, Chat, Tracks (project references the module's main RCL so it can inject services and use the module's DTOs):

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DotNetCloud.Modules.<Module>.Widget</RootNamespace>
    <AssemblyName>DotNetCloud.Modules.<Module>.Widget</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\DotNetCloud.Modules.<Module>\DotNetCloud.Modules.<Module>.csproj" />
    <ProjectReference Include="..\..\..\UI\DotNetCloud.UI.Shared\DotNetCloud.UI.Shared.csproj" />
  </ItemGroup>

</Project>
```

(Adjust the relative path to `UI.Shared` if a module folder is not exactly two levels below `src/Modules` — it is for all listed modules.)

### 7.2 csproj — process-isolated modules

For Calendar, Contacts, Bookmarks, Email, AI (interfaces + DTOs live in `DotNetCloud.Core`, so reference Core instead of the module project):

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\..\Core\DotNetCloud.Core\DotNetCloud.Core.csproj" />
    <ProjectReference Include="..\..\..\UI\DotNetCloud.UI.Shared\DotNetCloud.UI.Shared.csproj" />
  </ItemGroup>
```

### 7.3 `_Imports.razor` (identical for all widget projects)

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Authorization
@using DotNetCloud.UI.Shared.Components.DataDisplay
```

### 7.4 Widget `.razor` skeleton

```razor
@namespace DotNetCloud.Modules.<Module>.Widget

@if (_loading)
{
    <div class="widget-loading">Loading…</div>
}
else if (_error is not null)
{
    <div class="widget-empty">@_error</div>
}
else if (_items.Count == 0)
{
    <div class="widget-empty">@EmptyMessage</div>
}
else
{
    <ul class="widget-list">
        @foreach (var item in _items)
        {
            <li class="widget-list-item">@item.Title</li>
        }
    </ul>
}

@code {
    // code-behind partial: <Module>Widget.razor.cs
}
```

### 7.5 Widget `.razor.cs` code-behind skeleton (in-process modules)

```csharp
using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.<Module>.Widget;

/// <summary>
/// Home-page widget for the <Module> module.
/// </summary>
public partial class <Module>Widget : ComponentBase
{
    [Inject] private I<X>Service Service { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private List<<X>Dto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No <items> yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var result = await Service.GetRecent...Async(caller, 5);
            _items = result.ToList();
        }
        catch (Exception)
        {
            _error = "Unable to load <module> data.";
        }
        finally
        {
            _loading = false;
        }
    }

    // BuildCallerAsync from section 6.3
}
```

Process-isolated widgets differ only in the load method (no `CallerContext`):

```csharp
var result = await ApiClient.GetRecent...Async(5);
_items = result.ToList();
```

---

## 8. Phase 1 — Widgets reusing existing queries (no server changes)

These are the simplest. Build them in any order, following section 7.

### 8.1 Files widget — `src/Modules/Files/DotNetCloud.Modules.Files.Widget/`

- Inject `DotNetCloud.Modules.Files.Services.IFileService` and `DotNetCloud.Modules.Files.Services.IQuotaService`.
- Load:
  - `var recent = await FileService.ListRecentAsync(5, caller);`
  - `var quota = await QuotaService.GetOrCreateQuotaAsync(caller.UserId, caller);`
- Render: a small quota bar (used/max) at the top, then a list of the 5 recent files. `FileNodeDto` has `Name` and a modified timestamp — use the property for "modified" (check `FileNodeDto` in `src/Modules/Files/DotNetCloud.Modules.Files/DTOs/` for the exact name, likely `UpdatedAt`/`ModifiedAt`). Show a "No files yet." empty state.
- The existing `QuotaProgressBar` component (`DotNetCloud.Modules.Files.UI.QuotaProgressBar`) takes a `QuotaViewModel` parameter, so reusing it requires mapping `QuotaDto` → `QuotaViewModel`. Simplest: render an inline bar yourself from `quota.UsedBytes` / `quota.MaxBytes` (`width: {percent}%`), clamped to 100%. If you prefer, reuse `QuotaProgressBar` by constructing a `QuotaViewModel` (see `src/Modules/Files/DotNetCloud.Modules.Files/UI/ViewModels.cs`).
- Title "Files", icon `"folder"`, href `/apps/files`.

### 8.2 Video widget — `src/Modules/Video/DotNetCloud.Modules.Video.Widget/`

- Inject `DotNetCloud.Modules.Video.Services.IVideoService`.
- Load: `await VideoService.GetRecentVideosAsync(caller, 0, 5);` (returns newest first).
- Render 5 `VideoDto.Title` items. Empty: "No videos yet."
- Title "Video", icon `"movie"`, href `/apps/video`.

### 8.3 Music widget — `src/Modules/Music/DotNetCloud.Modules.Music.Widget/`

- Inject `DotNetCloud.Modules.Music.Services.IMusicAlbumService`.
- Load: `await AlbumService.GetRecentAlbumsAsync(caller, 5);`
- Render 5 `MusicAlbumDto` titles (use the album title property on `MusicAlbumDto`, e.g. `Title`/`Name` — verify in `src/Core/DotNetCloud.Core/DTOs/`). Empty: "No albums yet."
- Title "Music", icon `"music_note"`, href `/apps/music`.

---

## 9. Phase 2 — New in-process "recent" methods + widgets

For Photos, Notes, Chat the method is added to the **interface in the module main project** and the **implementation in the module `.Data` project**. No proto/gRPC changes (UI renders in-process). For Tracks, add a method to the concrete `WorkItemService` in `.Data`.

### 9.1 Photos

1. In `src/Modules/Photos/DotNetCloud.Modules.Photos/Services/IPhotoService.cs` add:

```csharp
/// <summary>Gets the most recently added photos for the caller.</summary>
Task<IReadOnlyList<PhotoDto>> GetRecentPhotosAsync(CallerContext caller, int count = 5, CancellationToken cancellationToken = default);
```

2. In `src/Modules/Photos/DotNetCloud.Modules.Photos.Data/Services/PhotoService.cs` implement it by mirroring `ListPhotosAsync` but `.Take(count)`:

```csharp
public async Task<IReadOnlyList<PhotoDto>> GetRecentPhotosAsync(CallerContext caller, int count = 5, CancellationToken cancellationToken = default)
{
    var photos = await _db.Photos
        .Include(p => p.Metadata)
        .Include(p => p.Tags)
        .Where(p => p.OwnerId == caller.UserId)
        .OrderByDescending(p => p.TakenAt)
        .Take(count)
        .ToListAsync(cancellationToken);
    return photos.Select(MapToDto).ToList();
}
```

3. Build the Photos widget (`src/Modules/Photos/DotNetCloud.Modules.Photos.Widget/`): inject `IPhotoService`, call `GetRecentPhotosAsync(caller, 5)`, render titles (or small thumbnails if a thumbnail URL is trivially available — otherwise titles). Title "Photos", icon `"photo"`, href `/apps/photos`.

### 9.2 Notes

1. In `src/Modules/Notes/DotNetCloud.Modules.Notes/Services/INoteService.cs` add:

```csharp
/// <summary>Gets the most recently edited notes for the caller.</summary>
Task<IReadOnlyList<NoteDto>> GetRecentNotesAsync(CallerContext caller, int count = 5, CancellationToken cancellationToken = default);
```

2. In `src/Modules/Notes/DotNetCloud.Modules.Notes.Data/Services/NoteService.cs` implement by mirroring `ListNotesAsync` (which already applies the soft-delete `QueryNotes()` filter and shares access) but ordering by `UpdatedAt` desc and `.Take(count)`:

```csharp
var notes = await QueryNotes()
    .Where(n => n.OwnerId == caller.UserId || n.Shares.Any(s => s.SharedWithUserId == caller.UserId))
    .OrderByDescending(n => n.UpdatedAt)
    .Take(count)
    .ToListAsync(cancellationToken);
return notes.Select(MapToDto).ToList();
```

3. Build the Notes widget (`src/Modules/Notes/DotNetCloud.Modules.Notes.Widget/`): inject `INoteService`, call `GetRecentNotesAsync(caller, 5)`, render `NoteDto.Title`. Title "Notes", icon `"note"`, href `/apps/notes`.

### 9.3 Chat

1. In `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IChannelService.cs` add:

```csharp
/// <summary>Gets the caller's most recently active channels.</summary>
Task<IReadOnlyList<ChannelDto>> GetRecentChannelsAsync(CallerContext caller, int count = 5, CancellationToken cancellationToken = default);
```

2. In the Chat `.Data` channel service implementation (locate the class implementing `IChannelService`, in `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/`), implement by mirroring `ListChannelsAsync` but ordering by `LastActivityAt` desc and `.Take(count)`. Exclude archived channels if `ListChannelsAsync` does. Reuse the same `MapToDto` helper.

3. Build the Chat widget (`src/Modules/Chat/DotNetCloud.Modules.Chat.Widget/`): inject `IChannelService`, call `GetRecentChannelsAsync(caller, 5)`, render `ChannelDto.Name`. Title "Chat", icon `"chat"`, href `/apps/chat`.

### 9.4 Tracks

1. In `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/WorkItemService.cs` (a concrete class; no interface) add:

```csharp
/// <summary>
/// Gets work items assigned to or watched by the user whose due dates fall within the
/// next <paramref name="daysAhead"/> days and whose swimlane is not done.
/// </summary>
public async Task<List<WorkItemDto>> GetMyUpcomingDueItemsAsync(
    Guid userId, int count = 5, int daysAhead = 14, CancellationToken ct = default)
```

2. Implementation guidance (mirror the existing query + mapping style in this file; `WorkItem` has `Assignments`, `Watchers`, `DueDate`, `IsArchived`, `IsDeleted`, and `Swimlane.IsDone`):

```csharp
var now = DateTime.UtcNow;
var horizon = now.Date.AddDays(daysAhead);
var items = await _db.WorkItems
    .Where(wi => !wi.IsArchived && !wi.IsDeleted
        && wi.DueDate.HasValue
        && wi.DueDate.Value >= now
        && wi.DueDate.Value <= horizon
        && (wi.Assignments.Any(a => a.UserId == userId) || wi.Watchers.Any(w => w.UserId == userId))
        && (wi.Swimlane == null || !wi.Swimlane.IsDone))
    .OrderBy(wi => wi.DueDate)
    .Take(count)
    .ToListAsync(ct);
return items.Select(MapToDto).ToList();
```

Reuse the existing private `MapToDto` used by `GetWorkItemsBySwimlaneAsync` (verify its exact name/visibility; if it is a different helper, use whatever maps `WorkItem` → `WorkItemDto`). Also verify the assignment navigation property name (`WorkItemAssignment.UserId` — check `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/WorkItemAssignment.cs`).

3. Build the Tracks widget (`src/Modules/Tracks/DotNetCloud.Modules.Tracks.Widget/`): inject the concrete `WorkItemService` (registered by `AddTracksUiServices`), resolve the current user id from claims (reuse the `BuildCallerAsync` pattern but you only need the `Guid` user id), call `GetMyUpcomingDueItemsAsync(userId, 5)`, render each item's `Title` + `DueDate` (format as date). Title "Tracks", icon `"task_alt"`, href `/apps/tracks`.

---

## 10. Phase 3 — Process-isolated modules: dedicated method + full gRPC chain

Each of Calendar, Contacts, Bookmarks, Email, AI requires 5 edits + 1 widget project. Do them one module at a time and rebuild frequently.

The pattern (using Contacts as the worked example):

### 10.1 Contacts (worked example)

**(a) Service interface + implementation**

- Interface `IContactService` lives in `src/Modules/Contacts/DotNetCloud.Modules.Contacts/Services/IContactService.cs`. Add:

```csharp
/// <summary>Gets the caller's most recently created contacts.</summary>
Task<IReadOnlyList<ContactDto>> GetRecentContactsAsync(CallerContext caller, int count = 5, CancellationToken cancellationToken = default);
```

- Implementation `ContactService` in `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data/Services/ContactService.cs`. Add a method that mirrors `ListContactsAsync` but orders by `CreatedAt` descending and `.Take(count)`. `Contact` has `CreatedAt`/`UpdatedAt` and a `MapToDto` helper already exists — reuse it:

```csharp
public async Task<IReadOnlyList<ContactDto>> GetRecentContactsAsync(CallerContext caller, int count = 5, CancellationToken cancellationToken = default)
{
    var contacts = await QueryContacts()
        .Where(c => c.OwnerId == caller.UserId || c.Shares.Any(s => s.SharedWithUserId == caller.UserId))
        .OrderByDescending(c => c.CreatedAt)
        .Take(count)
        .ToListAsync(cancellationToken);
    return contacts.Select(MapToDto).ToList();
}
```

(If `Contact` has no `CreatedAt` property but has `UpdatedAt`, order by `UpdatedAt` instead — verify the entity fields.)

**(b) Proto** — `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/Protos/contacts_service.proto`

Add request/response messages and an RPC. Follow the existing message naming/style in this file. Add:

```proto
message GetRecentContactsRequest {
  string user_id = 1;
  int32 count = 2;
}

message GetRecentContactsResponse {
  bool success = 1;
  repeated ContactMessage contacts = 2;
}

// inside the service definition:
rpc GetRecentContacts (GetRecentContactsRequest) returns (GetRecentContactsResponse);
```

(`ContactMessage` is the existing per-contact message type in that proto — use whatever the proto already calls it.)

**(c) gRPC service** — locate the Contacts gRPC service implementation (derives from `ContactsService.ContactsServiceBase`, in `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/Services/`). Override `GetRecentContacts`, resolve the caller (mirror how existing RPCs map the request `user_id` into a `CallerContext`), call `_contactService.GetRecentContactsAsync(...)`, map results into the response messages.

**(d) Client interface** — `src/Core/DotNetCloud.Core/Services/ModuleApis/IContactsApiClient.cs`

```csharp
/// <summary>Gets the current user's most recently created contacts.</summary>
Task<IReadOnlyList<ContactDto>> GetRecentContactsAsync(int count = 5, CancellationToken cancellationToken = default);
```

**(e) gRPC client** — `src/Core/DotNetCloud.Core.Server/Grpc/Clients/ContactsGrpcApiClient.cs`

Add a method following the existing `ListContactsAsync` implementation (uses `SafeCallAsync`, `GetUserId()`, `DeadlineHeaders`):

```csharp
public async Task<IReadOnlyList<ContactDto>> GetRecentContactsAsync(int count = 5, CancellationToken cancellationToken = default)
    => (await SafeCallAsync(async () =>
    {
        var request = new GetRecentContactsRequest { UserId = GetUserId(), Count = count };
        var response = await _client.Value.GetRecentContactsAsync(request, DeadlineHeaders(cancellationToken)).ResponseAsync;
        return !response.Success ? [] : response.Contacts.Select(c => ToContactDto(c)!).Where(c => c is not null).Select(c => c!).ToList();
    }, "GetRecentContacts", []))!;
```

(Reuse the existing `ToContactDto` mapping helper in that file.)

**(f) Widget** — `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Widget/` (csproj per section 7.2; injects `IContactsApiClient`; NO CallerContext). Load `await ApiClient.GetRecentContactsAsync(5)`, render `ContactDto.DisplayName`. Title "Contacts", icon `"contacts"`, href `/apps/contacts`.

### 10.2 Calendar

- **Service:** add `GetUpcomingEventsAsync` to `ICalendarEventService` (`src/Modules/Calendar/DotNetCloud.Modules.Calendar/Services/ICalendarEventService.cs`) and implement in `CalendarEventService` (`src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/Services/CalendarEventService.cs`). It must aggregate across all calendars the caller owns or has shared with them, filter events whose start is within `[fromUtc, toUtc]`, order by start ascending, take `count`:

```csharp
Task<IReadOnlyList<CalendarEventDto>> GetUpcomingEventsAsync(
    CallerContext caller, DateTime fromUtc, DateTime toUtc, int count = 5, CancellationToken cancellationToken = default);
```

- **Proto:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Protos/calendar_service.proto` — add `GetUpcomingEventsRequest { string user_id; string from_utc; string to_utc; int32 count; }`, `GetUpcomingEventsResponse { bool success; repeated CalendarEventMessage events; }`, and the RPC. (Match the existing event message name; timestamps as ISO strings like the rest of the proto.)
- **gRPC service:** locate the Calendar gRPC service implementation in `.Host/Services/` and implement the RPC.
- **Client interface:** add to `src/Core/DotNetCloud.Core/Services/ModuleApis/ICalendarApiClient.cs`:

```csharp
Task<IReadOnlyList<CalendarEventDto>> GetUpcomingEventsAsync(DateTime fromUtc, DateTime toUtc, int count = 5, CancellationToken cancellationToken = default);
```

- **gRPC client:** add to `src/Core/DotNetCloud.Core.Server/Grpc/Clients/CalendarGrpcApiClient.cs` (mirror its existing event-list method).
- **Widget:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Widget/` — injects `ICalendarApiClient`; calls with `DateTime.UtcNow` and `DateTime.UtcNow.AddDays(7)`; renders `CalendarEventDto.Title` + start time. Empty: "No upcoming events." Title "Calendar", icon `"event"`, href `/apps/calendar`.

### 10.3 Bookmarks

- **Service:** `BookmarkService` is concrete in `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Data/Services/BookmarkService.cs`. Add:

```csharp
public async Task<IReadOnlyList<BookmarkItemDto>> GetRecentBookmarksAsync(CallerContext caller, int count = 5, CancellationToken ct = default)
```

Mirror its `ListAsync` query (owner-scoped, non-deleted) but `OrderByDescending(b => b.CreatedAt).Take(count)`; reuse its existing mapping helper.

- **Proto:** `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Protos/bookmarks_service.proto` — `GetRecentBookmarksRequest { string user_id; int32 count; }` / `GetRecentBookmarksResponse { bool success; repeated BookmarkItemMessage bookmarks; }` + RPC (match existing bookmark message name).
- **gRPC service:** implement in the Bookmarks gRPC service in `.Host/Services/`.
- **Client interface:** add to `src/Core/DotNetCloud.Core/Services/ModuleApis/IBookmarksApiClient.cs`:

```csharp
Task<IReadOnlyList<BookmarkItemDto>> GetRecentBookmarksAsync(int count = 5, CancellationToken ct = default);
```

- **gRPC client:** add to `src/Core/DotNetCloud.Core.Server/Grpc/Clients/BookmarksGrpcApiClient.cs`.
- **Widget:** `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Widget/` — injects `IBookmarksApiClient`; renders `BookmarkItemDto.Title` (fall back to `Url` if title empty). Title "Bookmarks", icon `"bookmark"`, href `/apps/bookmarks`.

### 10.4 Email

- **Service:** locate where threads are queried for `ListThreadsAsync` (search the Email `.Data` services and the Email gRPC service for the existing thread query; likely `EmailAccountService` or direct DbContext in the gRPC service). Add a `GetRecentThreadsAsync` method on the appropriate service, ordering by thread date descending, `.Take(count)`, scoped to the caller's accounts/mailboxes.
- **Proto:** `src/Modules/Email/DotNetCloud.Modules.Email.Host/Protos/email_service.proto` — `GetRecentThreadsRequest { string user_id; int32 count; }` / `GetRecentThreadsResponse { bool success; repeated EmailThreadMessage threads; }` + RPC.
- **gRPC service:** implement in the Email gRPC service in `.Host/Services/`.
- **Client interface:** add to `src/Core/DotNetCloud.Core/Services/ModuleApis/IEmailApiClient.cs`:

```csharp
Task<IReadOnlyList<EmailThreadDto>> GetRecentThreadsAsync(int count = 5, CancellationToken ct = default);
```

- **gRPC client:** add to `src/Core/DotNetCloud.Core.Server/Grpc/Clients/EmailGrpcApiClient.cs`.
- **Widget:** `src/Modules/Email/DotNetCloud.Modules.Email.Widget/` — injects `IEmailApiClient`; first call `ListAccountsAsync()`; if no accounts, render "No account configured". Otherwise call `GetRecentThreadsAsync(5)` and render `EmailThreadDto.Subject`. Title "Email", icon `"mail"`, href `/apps/email`.

### 10.5 AI (count + last activity only — no titles)

- **Service:** add to `IAiChatService` (`src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiChatService.cs`) and implement in `AiChatService` (`src/Modules/AI/DotNetCloud.Modules.AI.Data/Services/AiChatService.cs`):

```csharp
Task<AiConversationStatsDto> GetConversationStatsAsync(Guid userId, CancellationToken ct = default);
```

The stats DTO carries `int TotalConversations` and `DateTime? LastActivityAt` (max of conversation `UpdatedAt`). Define `AiConversationStatsDto` near the AI DTOs (or in `IAiChatService.cs`).

- **Proto:** `src/Modules/AI/DotNetCloud.Modules.AI.Host/Protos/ai_service.proto` — `GetConversationStatsRequest { string user_id; }` / `GetConversationStatsResponse { bool success; int32 total_conversations; string last_activity_utc; }` + RPC.
- **gRPC service:** implement in the AI gRPC service in `.Host/Services/`.
- **Client interface:** add to `src/Core/DotNetCloud.Core/Services/ModuleApis/IAiApiClient.cs`:

```csharp
Task<ConversationStatsDto?> GetConversationStatsAsync(Guid userId, CancellationToken ct = default);
```

Define `ConversationStatsDto` (TotalConversations + LastActivityAt) in the Core AI DTO area used by `IAiApiClient`.

- **gRPC client:** add to `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AiGrpcApiClient.cs`.
- **Widget:** `src/Modules/AI/DotNetCloud.Modules.AI.Widget/` — injects `IAiApiClient`; resolves user id from claims; calls `GetConversationStatsAsync(userId)`; renders "N conversations" and "Last activity: <relative/date>". No conversation titles. Title "AI Assistant", icon `"smart_toy"`, href `/apps/ai`.

---

## 11. Phase 4 — Solution & wiring

### 11.1 Add projects to the solution

Run (from repo root):

```bash
dotnet sln DotNetCloud.sln add \
  src/Modules/Files/DotNetCloud.Modules.Files.Widget/DotNetCloud.Modules.Files.Widget.csproj \
  ... (one per widget project, all 12) ...
```

### 11.2 Add projects to the CI solution filter

`DotNetCloud.CI.slnf` is an explicit project list using **backslash Windows-style paths** (see its `"projects"` array). Add one entry per widget project in the same backslash format, e.g.:

```json
"src\\Modules\\Files\\DotNetCloud.Modules.Files.Widget\\DotNetCloud.Modules.Files.Widget.csproj",
```

Add all 12 entries (keep alphabetical-ish grouping like the existing list, or just append; either works as long as paths are correct).

### 11.3 Add ProjectReferences to Core.Server

In `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`, inside the existing `<!-- Module main (RCL) projects retained for Blazor UI component registration. -->` item group (or right after the other module ProjectReferences), add one `<ProjectReference>` per widget project, e.g.:

```xml
<ProjectReference Include="..\..\Modules\Files\DotNetCloud.Modules.Files.Widget\DotNetCloud.Modules.Files.Widget.csproj" />
```

This is required so `WidgetUiRegistrationHostedService` can reference `typeof(DotNetCloud.Modules.Files.Widget.FilesWidget)`.

### 11.4 Populate `KnownWidgetDescriptors`

In `WidgetUiRegistrationHostedService` (section 6.4), define the 12 descriptors. Use:

| ModuleId              | Title        | Icon       | Href            | SortOrder | ComponentType                                                  |
| --------------------- | ------------ | ---------- | --------------- | --------- | -------------------------------------------------------------- |
| dotnetcloud.files     | Files        | folder     | /apps/files     | 10        | `typeof(DotNetCloud.Modules.Files.Widget.FilesWidget)`         |
| dotnetcloud.chat      | Chat         | chat       | /apps/chat      | 20        | `typeof(DotNetCloud.Modules.Chat.Widget.ChatWidget)`           |
| dotnetcloud.contacts  | Contacts     | contacts   | /apps/contacts  | 30        | `typeof(DotNetCloud.Modules.Contacts.Widget.ContactsWidget)`   |
| dotnetcloud.calendar  | Calendar     | event      | /apps/calendar  | 40        | `typeof(DotNetCloud.Modules.Calendar.Widget.CalendarWidget)`   |
| dotnetcloud.notes     | Notes        | note       | /apps/notes     | 50        | `typeof(DotNetCloud.Modules.Notes.Widget.NotesWidget)`         |
| dotnetcloud.tracks    | Tracks       | task_alt   | /apps/tracks    | 60        | `typeof(DotNetCloud.Modules.Tracks.Widget.TracksWidget)`       |
| dotnetcloud.photos    | Photos       | photo      | /apps/photos    | 70        | `typeof(DotNetCloud.Modules.Photos.Widget.PhotosWidget)`       |
| dotnetcloud.music     | Music        | music_note | /apps/music     | 80        | `typeof(DotNetCloud.Modules.Music.Widget.MusicWidget)`         |
| dotnetcloud.video     | Video        | movie      | /apps/video     | 90        | `typeof(DotNetCloud.Modules.Video.Widget.VideoWidget)`         |
| dotnetcloud.ai        | AI Assistant | smart_toy  | /apps/ai        | 100       | `typeof(DotNetCloud.Modules.AI.Widget.AiWidget)`               |
| dotnetcloud.bookmarks | Bookmarks    | bookmark   | /apps/bookmarks | 110       | `typeof(DotNetCloud.Modules.Bookmarks.Widget.BookmarksWidget)` |
| dotnetcloud.email     | Email        | mail       | /apps/email     | 120       | `typeof(DotNetCloud.Modules.Email.Widget.EmailWidget)`         |

Use these exact module ids (they must match `InstalledModules.ModuleId`).

---

## 12. Phase 5 — Tests & documentation

### 12.1 Tests

- Add unit tests for each new service method in the matching test project:
  - `tests/DotNetCloud.Modules.Photos.Tests` → `GetRecentPhotosAsync`
  - `tests/DotNetCloud.Modules.Notes.Tests` → `GetRecentNotesAsync`
  - `tests/DotNetCloud.Modules.Chat.Tests` → `GetRecentChannelsAsync`
  - `tests/DotNetCloud.Modules.Tracks.Tests` → `GetMyUpcomingDueItemsAsync`
  - `tests/DotNetCloud.Modules.Contacts.Tests` → `GetRecentContactsAsync`
  - `tests/DotNetCloud.Modules.Calendar.Tests` → `GetUpcomingEventsAsync`
  - `tests/DotNetCloud.Modules.Bookmarks.Tests` → `GetRecentBookmarksAsync`
  - Email/AI test projects (if present) → the new email/AI methods.
- Follow the existing Arrange-Act-Assert test style in those projects (in-memory DB contexts). Test: returns newest-first ordering, respects `count`, excludes deleted/archived items (where applicable), and returns an empty list when there is no data.
- Add a small `WidgetUiRegistry` unit test (register, re-register is idempotent, unregister removes) if there is a suitable web-services test project (`tests/DotNetCloud.Core.Server.Tests`); otherwise a smoke test is optional.

### 12.2 Documentation (mandatory, targeted edits)

After code is complete and builds:

1. `docs/IMPLEMENTATION_CHECKLIST.md` — add/mark a "Module Home Widgets" section with `✓`/`☐` checkboxes (never `[x]`/`[ ]`).
2. `docs/MASTER_PROJECT_PLAN.md` — update the Quick Status Summary table and add a step section for this feature with Status/Deliverables/Notes.

---

## 13. Verification (run in order)

```bash
# 1. Restore & build (CI filter avoids Android SDK requirement)
dotnet build DotNetCloud.CI.slnf -c Release

# 2. Run affected tests
dotnet test tests/DotNetCloud.Modules.Contacts.Tests
dotnet test tests/DotNetCloud.Modules.Calendar.Tests
dotnet test tests/DotNetCloud.Modules.Bookmarks.Tests
dotnet test tests/DotNetCloud.Modules.Tracks.Tests
dotnet test tests/DotNetCloud.Modules.Photos.Tests
dotnet test tests/DotNetCloud.Modules.Notes.Tests
# plus Email/AI/Chat/Files test projects as they exist
```

Then (production deploy is on a Linux server; see repo memory):

```bash
# 3. Deploy
sudo ./scripts/deploy.sh
sudo ./scripts/deploy.sh --verify
```

Manual verification:

- Log in and open `/`. Confirm the top summary cards still render, "Your Apps" is gone, and all 12 widget cards render with data (or correct empty states).
- Each widget's "Open" link navigates to the correct `/apps/...` route.
- A module whose widget data is missing shows its empty message, not an error.
- Disabling a module in the admin UI removes its widget from Home (the hosted service refreshes within ~15s).
- Email widget shows "No account configured" when the user has no email account.
- AI widget shows count + last activity and no conversation titles.

---

## 14. Definition of done

- [ ] 12 widget projects created and referenced by Core.Server.
- [ ] `WidgetUiRegistry`, `WidgetCard`, and `WidgetUiRegistrationHostedService` exist and are wired in `Program.cs`.
- [ ] `Home.razor` no longer renders "Your Apps" and renders the widget grid.
- [ ] All new "recent"/stats server methods implemented (in-process + gRPC chains) with XML docs.
- [ ] Solution + CI filter include all new projects.
- [ ] `dotnet build DotNetCloud.CI.slnf -c Release` succeeds with zero warnings/errors.
- [ ] Relevant unit tests added and passing.
- [ ] `docs/IMPLEMENTATION_CHECKLIST.md` and `docs/MASTER_PROJECT_PLAN.md` updated with targeted edits.
- [ ] Deployed and manually verified end-to-end (per the commit policy: build → test → deploy → user verifies → only then commit).

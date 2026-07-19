# Android Chat Channel Mute & Notification Suppression — Implementation Plan

> **Branch:** `feature/android-chat-channel-mute`
> **Target:** Add per-channel mute toggles to the Android chat channel list, suppress Android system notifications for muted channels, and suppress all chat notifications when the app is in the foreground.
> **Server handoff:** cloud.kimball.home
> **Status:** Planning — ready for implementation

---

## Architecture Overview

Three new services plus modifications to existing notification paths:

```
ChannelListViewModel ──toggle──→ IChatRestClient.MuteChannelAsync / UnmuteChannelAsync
        │                              │
        │                              ▼
        │                     Server: POST/DELETE /api/v1/chat/channels/{id}/mute
        │
        ▼
ChannelMuteStateService (singleton cache: Guid → bool)
        │
        ├── consulted by ── FcmMessagingService.ShowChatNotification()
        └── consulted by ── UnifiedPushReceiver.ShowNotification()

AppForegroundService (singleton: bool IsInForeground)
        │
        ├── consulted by ── FcmMessagingService.ShowChatNotification()
        └── consulted by ── UnifiedPushReceiver.ShowNotification()

Notification decision tree:
  1. Is AppForegroundService.IsInForeground?  → YES: skip notification
  2. Is ChannelMuteStateService.IsMuted(channelId)?  → YES: skip notification
  3. Otherwise → post notification
```

**Mute state flow:**
```
Channel list loads → ChannelSummary.IsMuted (from server)
       │
       ├──→ ChannelItemViewModel.IsMuted   (UI binding)
       ├──→ ChannelMuteStateService cache   (notification lookup)
       └──→ ChannelDetailsViewModel.IsMuted (synced via event)
```

---

## Reference Files (study before implementing)

| File | What to learn |
|------|---------------|
| `src/Clients/DotNetCloud.Client.Android/Chat/IChatRestClient.cs` | REST interface pattern, DTO records (ChannelSummary, etc.) |
| `src/Clients/DotNetCloud.Client.Android/Chat/HttpChatRestClient.cs` | HTTP implementation, DTO mapping, envelope pattern |
| `src/Clients/DotNetCloud.Client.Android/ViewModels/ChannelListViewModel.cs` | ChannelItemViewModel, LoadChannels pattern, ObservableProperty |
| `src/Clients/DotNetCloud.Client.Android/Views/ChannelListPage.xaml` | Channel row DataTemplate layout |
| `src/Clients/DotNetCloud.Client.Android/Views/ChannelDetailsPage.xaml` | Existing mute switch UI (template to match) |
| `src/Clients/DotNetCloud.Client.Android/ViewModels/ChannelDetailsViewModel.cs` | Existing IsMuted property, member load pattern |
| `src/Clients/DotNetCloud.Client.Android/Platforms/Android/FcmMessagingService.cs` | Notification posting with foreground + mute checks to add |
| `src/Clients/DotNetCloud.Client.Android/Platforms/Android/UnifiedPushReceiver.cs` | F-Droid notification posting (same checks needed) |
| `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MainActivity.cs` | Lifecycle hooks for foreground tracking |
| `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs` | DI registration pattern |
| `src/Clients/DotNetCloud.Client.Android/Services/IAppPreferences.cs` | Key-value preferences pattern (reference only, not used directly) |

---

## Implementation Steps

### Step 1: Add `IsMuted` to Data Models

**File: `src/Clients/DotNetCloud.Client.Android/Chat/IChatRestClient.cs`**

1. Add `bool IsMuted = false` parameter to the `ChannelSummary` record, between `HasMention` and `LastMessagePreview`. Keep parameter ordering consistent — add it as a new parameter with default `false` so existing call sites still compile.

   ```csharp
   public sealed record ChannelSummary(
       Guid Id,
       string Name,
       int UnreadCount,
       bool HasMention,
       bool IsMuted,
       string? LastMessagePreview,
       DateTimeOffset? LastMessageAt);
   ```

2. Add two new methods to the `IChatRestClient` interface, immediately after `LeaveChannelAsync` and before the `// ── Image Upload ──` region comment:

   ```csharp
   // ── Notifications ──────────────────────────────────────────────────

   /// <summary>Mutes notifications for a channel. New messages will not trigger alerts.</summary>
   Task MuteChannelAsync(
       string serverBaseUrl, string accessToken,
       Guid channelId,
       CancellationToken ct = default);

   /// <summary>Unmutes notifications for a channel.</summary>
   Task UnmuteChannelAsync(
       string serverBaseUrl, string accessToken,
       Guid channelId,
       CancellationToken ct = default);
   ```

**File: `src/Clients/DotNetCloud.Client.Android/Chat/HttpChatRestClient.cs`**

1. Add `public bool IsMuted { get; init; }` to the `ChannelSummaryDto` class, after `HasMention`.

2. Update the `ToChannelSummary` mapping method to pass `d.IsMuted`:

   ```csharp
   private static ChannelSummary ToChannelSummary(ChannelSummaryDto d) =>
       new(d.Id, d.Name, d.UnreadCount, d.HasMention, d.IsMuted,
           d.LastMessagePreview,
           d.LastMessageAt ?? (d.LastActivityAt.HasValue
               ? new DateTimeOffset(d.LastActivityAt.Value, TimeSpan.Zero)
               : null));
   ```

3. Implement `MuteChannelAsync`:

   ```csharp
   /// <inheritdoc />
   public async Task MuteChannelAsync(
       string serverBaseUrl, string accessToken,
       Guid channelId, CancellationToken ct = default)
   {
       SetAuth(accessToken);
       var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/mute";
       using var response = await _http.PostAsync(url, null, ct).ConfigureAwait(false);
       if (!response.IsSuccessStatusCode)
           _logger.LogWarning("MuteChannel returned {StatusCode} for channel {ChannelId}.",
               response.StatusCode, channelId);
   }
   ```

4. Implement `UnmuteChannelAsync`:

   ```csharp
   /// <inheritdoc />
   public async Task UnmuteChannelAsync(
       string serverBaseUrl, string accessToken,
       Guid channelId, CancellationToken ct = default)
   {
       SetAuth(accessToken);
       var url = $"{serverBaseUrl.TrimEnd('/')}/api/v1/chat/channels/{channelId}/mute";
       using var response = await _http.DeleteAsync(url, ct).ConfigureAwait(false);
       if (!response.IsSuccessStatusCode)
           _logger.LogWarning("UnmuteChannel returned {StatusCode} for channel {ChannelId}.",
               response.StatusCode, channelId);
   }
   ```

**Verification:** `dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj` compiles without errors.

---

### Step 2: Update ChannelItemViewModel and ChannelListViewModel

**File: `src/Clients/DotNetCloud.Client.Android/ViewModels/ChannelListViewModel.cs`**

1. Add `IsMuted` observable property to `ChannelItemViewModel`. Add these lines after the existing `_hasMention` field:

   ```csharp
   /// <summary>Whether notifications for this channel are muted.</summary>
   [ObservableProperty] private bool _isMuted;
   ```

2. Update the `ChannelItemViewModel` constructor to accept and set `isMuted`:

   Change the constructor signature from:
   ```csharp
   public ChannelItemViewModel(Guid channelId, string name, int unreadCount, bool hasMention, string? lastMessagePreview)
   ```
   To:
   ```csharp
   public ChannelItemViewModel(Guid channelId, string name, int unreadCount, bool hasMention, bool isMuted, string? lastMessagePreview)
   ```

   And add `IsMuted = isMuted;` in the constructor body, after `HasMention = hasMention;`.

3. Update the `Channels.Add` call inside `LoadChannelsAsync` to pass `ch.IsMuted`:

   Change:
   ```csharp
   Channels.Add(new ChannelItemViewModel(ch.Id, ch.Name, ch.UnreadCount, ch.HasMention, ch.LastMessagePreview));
   ```
   To:
   ```csharp
   Channels.Add(new ChannelItemViewModel(ch.Id, ch.Name, ch.UnreadCount, ch.HasMention, ch.IsMuted, ch.LastMessagePreview));
   ```

4. Add `ToggleMuteCommand` to `ChannelListViewModel`. Add this method to the class (before the `Dispose` method):

   ```csharp
   /// <summary>Toggles the mute state for a channel.</summary>
   [RelayCommand]
   private async Task ToggleMuteAsync(ChannelItemViewModel item, CancellationToken ct)
   {
       try
       {
           var (serverUrl, token) = await GetActiveCredentialsAsync(ct);
           if (item.IsMuted)
               await _chatApi.UnmuteChannelAsync(serverUrl, token, item.ChannelId, ct);
           else
               await _chatApi.MuteChannelAsync(serverUrl, token, item.ChannelId, ct);

           item.IsMuted = !item.IsMuted;
           // Notify mute state change for ChannelDetailsViewModel sync
           MuteStateChanged?.Invoke(this, (item.ChannelId, item.IsMuted));
       }
       catch (Exception ex)
       {
           _logger.LogWarning(ex, "Failed to toggle mute for channel {ChannelId}.", item.ChannelId);
       }
   }
   ```

   Note: The `GetActiveCredentialsAsync` helper is already `private` and returns `(string serverUrl, string token)`. The `ToggleMuteAsync` method must be added inside the `ChannelListViewModel` class (not `ChannelItemViewModel`).

5. Add the `MuteStateChanged` event to `ChannelListViewModel`. Add this line after the `ChannelSelected` event:

   ```csharp
   /// <summary>Raised when a channel's mute state changes (used to sync with ChannelDetailsViewModel).</summary>
   public event EventHandler<(Guid ChannelId, bool IsMuted)>? MuteStateChanged;
   ```

**Verification:** `dotnet build` compiles. Tapping mute in the channel list will call the API (server may return 404 until cloud.kimball.home implements the endpoints — this is expected, the client-side is ready).

---

### Step 3: Update ChannelListPage XAML — Mute Icon Per Row

**File: `src/Clients/DotNetCloud.Client.Android/Views/ChannelListPage.xaml`**

Add a mute toggle icon to each channel row. The icon should appear in the Column 2 area alongside the unread badge, positioned to the right of it.

In the `DataTemplate` for `ChannelItemViewModel`, find the Grid column definitions. The row has `ColumnDefinitions="Auto,*,Auto"`. The unread badge is in Column 2. We'll add the mute icon also in Column 2, placed after the unread badge.

After the unread badge `Border` (which has `Grid.Column="2"`), add a mute toggle label. The mute toggle should be tappable and visually indicate mute state.

Add this XAML after the closing `</Border>` of the unread badge, and before the `<Grid.GestureRecognizers>`:

```xml
<!-- Mute toggle (visible when tapped channel row area; shows mute/unmute icon) -->
<Label
    Grid.Column="2"
    FontSize="14"
    HorizontalOptions="End"
    VerticalOptions="Center"
    TextColor="#94A3B8"
    Margin="4,0,0,0"
    Text="🔕"
    IsVisible="{Binding IsMuted}">
</Label>
<Label
    Grid.Column="2"
    FontSize="14"
    HorizontalOptions="End"
    VerticalOptions="Center"
    TextColor="#64748B"
    Margin="4,0,0,0"
    Text="🔔"
    IsVisible="{Binding IsMuted, Converter={StaticResource InvertBool}}">
</Label>
```

Then, add a tap gesture recognizer for the mute toggle. Add this inside the existing `<Grid.GestureRecognizers>` block for the channel row, right before the existing `TapGestureRecognizer` for selection:

```xml
<TapGestureRecognizer
    Command="{Binding Source={RelativeSource AncestorType={x:Type ContentPage}}, Path=BindingContext.ToggleMuteCommand}"
    CommandParameter="{Binding .}"
    NumberOfTapsRequired="2" />
```

Note: Using double-tap for mute avoids conflicting with the single-tap for channel selection. Alternatively, you can wrap only the mute icon labels with their own `TapGestureRecognizer`. The double-tap approach is simpler to implement.

**Verification:** Channel list shows 🔔 on unmuted channels and 🔕 on muted channels. Double-tapping a channel row toggles mute state. Single-tap still navigates to the channel.

---

### Step 4: Wire Mute Toggle in ChannelDetailsViewModel

**File: `src/Clients/DotNetCloud.Client.Android/ViewModels/ChannelDetailsViewModel.cs`**

The class already has `[ObservableProperty] private bool _isMuted;` and the XAML already has a `Switch` bound to it. But there is no logic that calls the API when the switch changes. Add a partial method to handle mute changes.

Add this method to the `ChannelDetailsViewModel` class:

```csharp
/// <summary>Called when <see cref="IsMuted"/> changes. Calls the server API.</summary>
partial void OnIsMutedChanged(bool value)
{
    if (_serverUrl is null || _accessToken is null)
        return;

    _ = Task.Run(async () =>
    {
        try
        {
            if (value)
                await _chatApi.MuteChannelAsync(_serverUrl, _accessToken, _channelId);
            else
                await _chatApi.UnmuteChannelAsync(_serverUrl, _accessToken, _channelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle mute for channel {ChannelId} from details page.", _channelId);
            // Revert the toggle on failure
            await MainThread.InvokeOnMainThreadAsync(() => IsMuted = !value);
        }
    });
}
```

To keep `ChannelDetailsViewModel` in sync with `ChannelListViewModel`, subscribe to `MuteStateChanged` when the details page is shown. This requires a small change to how `ChannelDetailsPage` passes data.

**File: `src/Clients/DotNetCloud.Client.Android/Views/ChannelDetailsPage.xaml.cs`**

In the `ChannelDetailsPage` code-behind, resolve the `ChannelListViewModel` and subscribe to `MuteStateChanged`:

```csharp
private ChannelListViewModel? _listVm;

protected override void OnAppearing()
{
    base.OnAppearing();
    _listVm = Handler?.MauiContext?.Services.GetService<ChannelListViewModel>();
    if (_listVm is not null)
        _listVm.MuteStateChanged += OnMuteStateChanged;
}

protected override void OnDisappearing()
{
    base.OnDisappearing();
    if (_listVm is not null)
        _listVm.MuteStateChanged -= OnMuteStateChanged;
}

private void OnMuteStateChanged(object? sender, (Guid ChannelId, bool IsMuted) e)
{
    if (BindingContext is ChannelDetailsViewModel vm && vm.ChannelId == e.ChannelId)
        vm.IsMuted = e.IsMuted;
}
```

Note: `ChannelDetailsViewModel.ChannelId` needs to be exposed. Add a public property:

```csharp
/// <summary>The current channel ID (exposed for cross-VM sync).</summary>
public Guid ChannelId => _channelId;
```

**Verification:** Toggling mute in the channel list updates the details page switch. Toggling the switch on the details page calls the API and updates the channel list.

---

### Step 5: Create IAppForegroundService

**New file: `src/Clients/DotNetCloud.Client.Android/Services/IAppForegroundService.cs`**

```csharp
namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Tracks whether the Android app is currently in the foreground (visible to the user).
/// Used to suppress notifications when the user is actively using the app.
/// </summary>
public interface IAppForegroundService
{
    /// <summary><c>true</c> when the app is visible to the user (at least one Activity is resumed).</summary>
    bool IsInForeground { get; }

    /// <summary>Raised when the foreground state changes.</summary>
    event EventHandler<bool>? ForegroundChanged;
}
```

**New file: `src/Clients/DotNetCloud.Client.Android/Services/AppForegroundService.cs`**

```csharp
namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Default implementation of <see cref="IAppForegroundService"/>.
/// Updated by <see cref="MainActivity"/> lifecycle callbacks.
/// </summary>
internal sealed class AppForegroundService : IAppForegroundService
{
    private volatile bool _isInForeground;

    /// <inheritdoc />
    public bool IsInForeground => _isInForeground;

    /// <inheritdoc />
    public event EventHandler<bool>? ForegroundChanged;

    /// <summary>
    /// Called by <see cref="MainActivity.OnResume"/> and <see cref="MainActivity.OnPause"/>.
    /// Thread-safe; fires <see cref="ForegroundChanged"/> on the calling thread.
    /// </summary>
    public void SetForeground(bool isInForeground)
    {
        if (_isInForeground == isInForeground)
            return;

        _isInForeground = isInForeground;
        ForegroundChanged?.Invoke(this, isInForeground);
    }
}
```

**File: `src/Clients/DotNetCloud.Client.Android/Platforms/Android/MainActivity.cs`**

Add `OnResume` and `OnPause` overrides. Resolve `IAppForegroundService` via `Ioc.Default`.

Add these using directives:
```csharp
using CommunityToolkit.Mvvm.DependencyInjection;
using DotNetCloud.Client.Android.Services;
```

Add these methods to the `MainActivity` class:

```csharp
/// <inheritdoc />
protected override void OnResume()
{
    base.OnResume();
    try
    {
        Ioc.Default.GetService<IAppForegroundService>()?.SetForeground(true);
    }
    catch { /* Best effort */ }
}

/// <inheritdoc />
protected override void OnPause()
{
    base.OnPause();
    try
    {
        Ioc.Default.GetService<IAppForegroundService>()?.SetForeground(false);
    }
    catch { /* Best effort */ }
}
```

**File: `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`**

Add the DI registration. After the `// ── Platform services ──` comment block, add:

```csharp
builder.Services.AddSingleton<IAppForegroundService, AppForegroundService>();
```

**Verification:** `dotnet build` compiles. `IAppForegroundService.IsInForeground` should be `true` when the app is visible.

---

### Step 6: Create IChannelMuteStateService

**New file: `src/Clients/DotNetCloud.Client.Android/Services/IChannelMuteStateService.cs`**

```csharp
namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Thread-safe cache of per-channel mute state.
/// Populated when channels are loaded from the server; consulted by notification handlers
/// to decide whether to suppress alerts for muted channels.
/// </summary>
public interface IChannelMuteStateService
{
    /// <summary>Returns <c>true</c> if the channel is muted.</summary>
    bool IsMuted(Guid channelId);

    /// <summary>Updates the mute state for a channel.</summary>
    void SetMuted(Guid channelId, bool isMuted);

    /// <summary>Replaces all entries with the given set (called on channel list refresh).</summary>
    void ReplaceAll(IReadOnlyDictionary<Guid, bool> states);
}
```

**New file: `src/Clients/DotNetCloud.Client.Android/Services/ChannelMuteStateService.cs`**

```csharp
using System.Collections.Concurrent;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Default implementation of <see cref="IChannelMuteStateService"/> using a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
internal sealed class ChannelMuteStateService : IChannelMuteStateService
{
    private readonly ConcurrentDictionary<Guid, bool> _muted = new();

    /// <inheritdoc />
    public bool IsMuted(Guid channelId) =>
        _muted.TryGetValue(channelId, out var isMuted) && isMuted;

    /// <inheritdoc />
    public void SetMuted(Guid channelId, bool isMuted) =>
        _muted[channelId] = isMuted;

    /// <inheritdoc />
    public void ReplaceAll(IReadOnlyDictionary<Guid, bool> states)
    {
        _muted.Clear();
        foreach (var kvp in states)
            _muted[kvp.Key] = kvp.Value;
    }
}
```

**File: `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`**

Add DI registration alongside the `IAppForegroundService` registration:

```csharp
builder.Services.AddSingleton<IChannelMuteStateService, ChannelMuteStateService>();
```

**File: `src/Clients/DotNetCloud.Client.Android/ViewModels/ChannelListViewModel.cs`**

Inject `IChannelMuteStateService` into `ChannelListViewModel`:

Add field:
```csharp
private readonly IChannelMuteStateService _muteState;
```

Update constructor to accept `IChannelMuteStateService muteState` and set `_muteState = muteState;`.

In `LoadChannelsAsync`, after clearing `Channels` and before the `foreach` loop, populate the mute state cache:

```csharp
var muteStates = new Dictionary<Guid, bool>();
foreach (var ch in channels)
    muteStates[ch.Id] = ch.IsMuted;
_muteState.ReplaceAll(muteStates);
```

In `ToggleMuteAsync`, after toggling `item.IsMuted`, update the cache:

```csharp
_muteState.SetMuted(item.ChannelId, item.IsMuted);
```

**Verification:** `dotnet build` compiles. `IChannelMuteStateService.IsMuted()` returns correct values after channel list loads.

---

### Step 7: Add Foreground + Mute Checks to Notification Handlers

**File: `src/Clients/DotNetCloud.Client.Android/Platforms/Android/FcmMessagingService.cs`**

At the top of the `ShowChatNotification` method, before building the intent or notification, add these checks:

```csharp
private void ShowChatNotification(string type, string? channelId, string title, string body)
{
    // ── Foreground check: suppress if app is visible ──
    try
    {
        var foreground = Ioc.Default.GetService<IAppForegroundService>();
        if (foreground?.IsInForeground == true)
        {
            var logger = Ioc.Default.GetService<ILogger<FcmMessagingService>>();
            logger?.LogDebug("App in foreground; suppressing notification for channel {ChannelId}.", channelId);
            return;
        }
    }
    catch { /* Best effort — post notification if we can't check */ }

    // ── Mute check: suppress if channel is muted ──
    try
    {
        if (Guid.TryParse(channelId, out var chId) && chId != Guid.Empty)
        {
            var muteState = Ioc.Default.GetService<IChannelMuteStateService>();
            if (muteState?.IsMuted(chId) == true)
            {
                var logger = Ioc.Default.GetService<ILogger<FcmMessagingService>>();
                logger?.LogDebug("Channel {ChannelId} is muted; suppressing notification.", channelId);
                return;
            }
        }
    }
    catch { /* Best effort */ }

    // ... existing notification-building code continues here ...
    var channelGuid = Guid.TryParse(channelId, out var g) ? g : Guid.Empty;
    // ...
}
```

You also need to add the using directive at the top of the file:
```csharp
using DotNetCloud.Client.Android.Services;
```

**File: `src/Clients/DotNetCloud.Client.Android/Platforms/Android/UnifiedPushReceiver.cs`**

Add the same foreground + mute checks at the top of the `ShowNotification` method, before building the intent. Use the same `Ioc.Default.GetService<>` pattern.

Add the same using directive:
```csharp
using DotNetCloud.Client.Android.Services;
```

**Verification:**
1. Send a chat message to an **unmuted** channel while app is backgrounded → Android notification appears
2. Send a chat message to a **muted** channel while app is backgrounded → no notification
3. Send a chat message (any channel) while app is in foreground → no notification
4. Bring app to foreground after receiving notifications → mute toggles still reflect correct state

---

### Step 8: DI Registration (MauiProgram.cs Summary)

**File: `src/Clients/DotNetCloud.Client.Android/MauiProgram.cs`**

All DI registrations needed (some may already exist, only add the NEW ones):

```csharp
// ── Foreground tracking ────────────────────────────────────────
builder.Services.AddSingleton<IAppForegroundService, AppForegroundService>();

// ── Mute state cache ───────────────────────────────────────────
builder.Services.AddSingleton<IChannelMuteStateService, ChannelMuteStateService>();
```

**Verification:** `dotnet build` compiles and all services resolve correctly at runtime.

---

## Server-Side API Contract (Handoff to cloud.kimball.home)

The following server-side changes are required for the mute feature to work end-to-end:

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/chat/channels` | Add `isMuted: boolean` to each channel in the response |
| `POST` | `/api/v1/chat/channels/{channelId}/mute` | Mute notifications for the current user on this channel |
| `DELETE` | `/api/v1/chat/channels/{channelId}/mute` | Unmute notifications for the current user on this channel |

### GET /api/v1/chat/channels Response Change

Add `isMuted` property to each channel object:

```json
{
  "success": true,
  "data": [
    {
      "id": "guid",
      "name": "string",
      "unreadCount": 0,
      "hasMention": false,
      "isMuted": false,
      "lastMessagePreview": "string?",
      "lastMessageAt": "datetime?"
    }
  ]
}
```

### Database Migration

Add a `ChatChannelMutePreferences` table (or equivalent):

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | Primary key |
| `UserId` | `Guid` | FK to AspNetUsers |
| `ChannelId` | `Guid` | FK to ChatChannels |
| `IsMuted` | `bool` | Default `false` |
| `MutedAt` | `DateTimeOffset` | When mute was last toggled |

Unique constraint on `(UserId, ChannelId)`.

### Business Logic

- `POST /mute`: Upsert — create a mute preference row if none exists, or set `IsMuted = true`. Return `204 No Content`.
- `DELETE /mute`: Set `IsMuted = false` or delete the row. Return `204 No Content`.
- `GET /channels`: For each channel, left-join to `ChatChannelMutePreferences` for the authenticated user to populate `isMuted`.

### Android Client Resilience

The Android client handles `404` and non-success status codes gracefully — it logs a warning but does not crash. This means the client can be deployed before the server endpoints are ready; mute toggles will simply have no server-side effect until cloud.kimball.home deploys.

---

## New Files Summary

| File | Purpose |
|------|---------|
| `docs/ANDROID_CHAT_MUTE_PLAN.md` | This plan document |
| `Services/IAppForegroundService.cs` | Foreground/background tracking interface |
| `Services/AppForegroundService.cs` | Foreground tracking implementation |
| `Services/IChannelMuteStateService.cs` | Mute state cache interface |
| `Services/ChannelMuteStateService.cs` | Mute state cache implementation (ConcurrentDictionary) |

## Modified Files Summary

| File | Changes |
|------|---------|
| `Chat/IChatRestClient.cs` | Add `IsMuted` to `ChannelSummary` record, add `MuteChannelAsync`/`UnmuteChannelAsync` to interface |
| `Chat/HttpChatRestClient.cs` | Add `IsMuted` to `ChannelSummaryDto`, update `ToChannelSummary`, implement mute/unmute |
| `ViewModels/ChannelListViewModel.cs` | Inject `IChannelMuteStateService`, update `ChannelItemViewModel` ctor, add `ToggleMuteCommand`, add `MuteStateChanged` event, populate mute cache on load |
| `ViewModels/ChannelDetailsViewModel.cs` | Add `OnIsMutedChanged` partial method to call API, expose `ChannelId` property |
| `Views/ChannelListPage.xaml` | Add mute/unmute toggle labels per channel row, add double-tap gesture for mute |
| `Views/ChannelDetailsPage.xaml.cs` | Subscribe to `MuteStateChanged` for cross-VM sync |
| `Platforms/Android/MainActivity.cs` | Add `OnResume`/`OnPause` overrides to update `IAppForegroundService` |
| `Platforms/Android/FcmMessagingService.cs` | Add foreground + mute checks before posting notifications |
| `Platforms/Android/UnifiedPushReceiver.cs` | Add foreground + mute checks before posting notifications |
| `MauiProgram.cs` | Register `IAppForegroundService` and `IChannelMuteStateService` as singletons |

---

## Build & Test Commands

```powershell
# Build the Android project
dotnet build src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj

# Build the entire solution
dotnet build

# Run all tests
dotnet test
```

---

## Edge Cases to Handle

1. **API call fails** — Mute toggle should revert to previous state (already handled in `OnIsMutedChanged` via try/catch + revert). For `ToggleMuteCommand`, the toggle is optimistic but API failure is logged — state stays in-memory until next channel list refresh corrects it.

2. **Channel list reload** — When `LoadChannelsAsync` runs (pull-to-refresh, app resume), it should overwrite mute states from the server response and repopulate the cache.

3. **Notification while app resumes** — There's a race window where the app is resuming but `IsInForeground` hasn't been set to `true` yet. The `OnResume` → `SetForeground(true)` call happens synchronously in the Activity lifecycle, so it should be set before the Activity processes any pending intents. If a race does occur, the notification would fire briefly — acceptable.

4. **Multiple Activities** — MAUI on Android typically has one Activity. `MainActivity.OnPause` fires when the app goes to background, `OnResume` when it returns. This is sufficient for Android foreground detection.

5. **Process death** — If Android kills the app process, `ChannelMuteStateService` cache is lost. On next cold start, `LoadChannelsAsync` repopulates it from the server response. No persistence needed client-side since mute is server-side.

6. **Offline behavior** — If the device is offline, mute toggles will fail silently. The local state changes optimistically. On next successful channel list load, the server state (unchanged) will overwrite. This is acceptable because the user will see the mute didn't actually take effect.

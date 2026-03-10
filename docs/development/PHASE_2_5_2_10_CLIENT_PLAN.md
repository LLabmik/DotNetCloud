# Phase 2.5–2.10 Client Implementation Plan

**Owner:** Windows workspace (client agent)  
**Started:** 2026-03-10  
**Status:** In progress

---

## Step 1 — Phase 2.5: Realtime UX Wiring in `ChatNotificationBadge`

**Gap:** `ChatNotificationBadge` is a pure parameter-driven component. The checklist item "Update in real time via SignalR" is ☐ unchecked.

**Work:**
- Add `SignalRChatService`-injected code-behind to subscribe to `UnreadCountUpdated` events and call `StateHasChanged`
- Wire unread count accumulation across all channels

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChatNotificationBadge.razor.cs`

**Tests:** Unit test for badge count increment on event

**Status:** ✓

---

## Step 2a — Phase 2.6: Announcement Filter by Priority and Date

**Gap:** `AnnouncementList` has no filter controls. Checklist item "Filter by priority and date" is ☐ unchecked.

**Work:**
- Add `SelectedPriority` (`All`, `Normal`, `Important`, `Urgent`) and `FromDate`/`ToDate` filter state to `AnnouncementList.razor.cs`
- Add a filter bar row to the markup
- Apply filters to `Announcements` before rendering

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/AnnouncementList.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/AnnouncementList.razor.cs`

**Status:** ☐

---

## Step 2b — Phase 2.6: Announcement Preview Before Publishing

**Gap:** `AnnouncementEditor` has no preview mode. Checklist item "Preview before publishing" is ☐ unchecked.

**Work:**
- Add `_isPreviewMode` boolean toggle to `AnnouncementEditor.razor.cs`
- Add "Edit / Preview" tab switcher in markup
- Preview tab renders content using safe Markdown rendering (same approach as `MessageList`)

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/AnnouncementEditor.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/AnnouncementEditor.razor.cs`

**Status:** ☐

---

## Step 3a — Phase 2.7: Push API Client + DTOs

**Gap:** `ChatApiClient` has no push registration or notification preference methods. Server endpoints `POST /api/v1/notifications/devices/register`, `DELETE /api/v1/notifications/devices/{token}`, `GET/PUT /api/v1/notifications/preferences` are complete.

**Work:**
- Add `RegisterDeviceDto` and `NotificationPreferencesDto` to `ChatDtos.cs`
- Add `RegisterDeviceAsync`, `UnregisterDeviceAsync`, `GetNotificationPreferencesAsync`, `UpdateNotificationPreferencesAsync` to `ChatApiClient`

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/DTOs/ChatDtos.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/ChatApiClient.cs`

**Status:** ☐

---

## Step 3b — Phase 2.7: Notification Preferences UI Panel

**Gap:** No client UI for push registration or notification preferences. Multiple 2.7 checklist items depend on this.

**Work:**
- Create `NotificationPreferencesPanel.razor` + `.razor.cs` settings panel with:
  - Global push enable/disable toggle
  - DND mode toggle
  - Per-channel mute list (channel name + mute checkbox)
- Wire to `ChatApiClient` GET/PUT preferences on load/save

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/NotificationPreferencesPanel.razor` *(new)*
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/NotificationPreferencesPanel.razor.cs` *(new)*

**Status:** ☐

---

## Step 4a — Phase 2.8: Markdown Toolbar in `MessageComposer`

**Gap:** Checklist item "Rich text input with Markdown toolbar" is ☐ unchecked.

**Work:**
- Add Bold / Italic / Code / Link toolbar buttons above the textarea
- Each button wraps selected text or inserts Markdown syntax at cursor position via JS interop
- Add `WrapSelectionAsync(prefix, suffix)` JS helper to `composer-toolbar.js`

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageComposer.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageComposer.razor.cs`
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/composer-toolbar.js` *(new)*

**Status:** ☐

---

## Step 4b — Phase 2.8: `@mention` Autocomplete in `MessageComposer`

**Gap:** Checklist item "`@mention` autocomplete" is ☐ unchecked.

**Work:**
- Detect `@` keystroke → show filtered member dropdown
- Filter `MentionSuggestions` (new parameter: `List<MemberViewModel>`) as user types after `@`
- On selection, insert `@DisplayName` into message text and close dropdown
- Hide dropdown on Escape or when text after `@` goes empty

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageComposer.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageComposer.razor.cs`

**Status:** ☐

---

## Step 4c — Phase 2.8: Paste Image Support in `MessageComposer`

**Gap:** Checklist item "Paste image support (auto-upload)" is ☐ unchecked.

**Work:**
- Handle `paste` event via JS interop to detect `image/*` clipboard items
- Extract image blob data and fire `OnPasteImage` callback (`EventCallback<PastedImageData>`)
- Caller handles upload via Files API; on upload complete, attach file ID to message send

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageComposer.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/MessageComposer.razor.cs`
- `src/UI/DotNetCloud.UI.Web/wwwroot/js/composer-toolbar.js` *(extend)*

**Status:** ☐

---

## Step 4d — Phase 2.8: Drag-to-Reorder Pinned Channels in `ChannelList`

**Gap:** Checklist item "Support drag-to-reorder pinned channels" is `[ ]` unchecked (uses `[ ]` vs `☐` in checklist — same gap).

**Work:**
- Add HTML5 `draggable` + `ondragstart`/`ondragover`/`ondrop` handlers to pinned channel items
- Maintain `_pinnedOrder` reorder state in `ChannelList.razor.cs`
- Fire `OnChannelReordered` callback on successful drop
- Visually show drag ghost and drop-target indicator via CSS class

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.cs`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/ChannelList.razor.css`

**Status:** ☐

---

## Step 4e — Phase 2.8: DM User Search (Start New DM)

**Gap:** Checklist item "User search for starting new DM" is ☐ unchecked.

**Work:**
- Add user search input to `DirectMessageView` (shown when no `OtherUser` is set, or via a "New DM" button)
- Filter against `UserSuggestions` parameter (injected from parent via `IUserDirectoryCapability` or similar)
- On user select, call `ChatApiClient.GetOrCreateDmAsync` and raise `OnDmChannelReady` callback

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/DirectMessageView.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/DirectMessageView.razor.cs`

**Status:** ☐

---

## Step 4f — Phase 2.8: Group DM Support in `DirectMessageView`

**Gap:** Checklist item "Group DM support (2+ users)" is ☐ unchecked.

**Work:**
- Add "Add people" button to DM header when `OtherUser` is set
- Show a member picker dropdown (from `UserSuggestions` parameter)
- On selection, call `ChatApiClient.AddMemberAsync` to add user to the DM channel
- Update header to show "Group" indicator when member count > 2

**Files:**
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/DirectMessageView.razor`
- `src/Modules/Chat/DotNetCloud.Modules.Chat/UI/DirectMessageView.razor.cs`

**Status:** ☐

---

## Step 5a — Phase 2.9: Chat Unread Badge on Tray Icon

**Gap:** `TrayViewModel` has no chat awareness. Checklist items for tray badge are `[ ]` unchecked.

**Work:**
- Add `IChatSignalRClient` interface to `DotNetCloud.Client.Core` (minimal: `ConnectAsync`, `OnUnreadCountUpdated` event, `OnNewChatMessage` event)
- Add `ChatUnreadCount` and `ChatHasMentions` properties to `TrayViewModel`
- Subscribe to `UnreadCountUpdated` events; accumulate total across channels
- Update `Tooltip` text to include unread chat summary when > 0

**Files:**
- `src/Clients/DotNetCloud.Client.Core/IChatSignalRClient.cs` *(new)*
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`

**Status:** ☐

---

## Step 5b — Phase 2.9: Chat Notification Popups

**Gap:** Checklist items "Add chat notification popups (Windows toast / Linux libnotify)" and "Display message preview in notification" are `[ ]` unchecked.

**Work:**
- Add `Chat` and `Mention` values to `NotificationType` enum
- Update `WindowsNotificationService` and `LinuxNotificationService` to use distinct icons/urgency for `Chat` vs `Mention`
- In `TrayViewModel`, subscribe to `NewChatMessage` events; call `ShowNotification` with sender + preview text
- Respect channel display name in notification title

**Files:**
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/NotificationType.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/WindowsNotificationService.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/LinuxNotificationService.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`

**Status:** ☐

---

## Step 5c — Phase 2.9: DND/Mute Respected in SyncTray

**Gap:** No DND/mute awareness in tray notification path. Checklist item "Respect DND/mute settings" is `[ ]` unchecked.

**Work:**
- Add `IsMuteChatNotifications` bool to `SettingsViewModel` (persisted in settings JSON)
- Check before calling `ShowNotification` in `TrayViewModel`

**Files:**
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`

**Status:** ☐

---

## Step 5d — Phase 2.9: Click Notification to Open Web Chat

**Gap:** No click handler to open the browser. Checklist item "Click notification to open chat in web browser" is `[ ]` unchecked.

**Work:**
- Add `OnNotificationActivated` callback to `INotificationService` (optional — platform-dependent)
- Implement in `WindowsNotificationService` using `Process.Start` with the server web URL + `/apps/chat`
- Linux: pass the open-action URL as `--action` to `notify-send` or implement `xdg-open`

**Files:**
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/INotificationService.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/WindowsNotificationService.cs`
- `src/Clients/DotNetCloud.Client.SyncTray/Notifications/LinuxNotificationService.cs`

**Status:** ☐

---

## Step 6 — Phase 2.9: Regression Checklist Pass

**Work:** Run full test suite and verify the following scenarios manually:

- ☐ Channel list loads; unread badges show; presence dots correct
- ☐ Message send/receive; reactions; typing indicator expires
- ☐ Announcement create / filter / preview / acknowledge
- ☐ Realtime badge update on new message (SignalR event path)
- ☐ DM + group DM flows (user search → channel creation → messaging)
- ☐ Push preference save/load (GET round-trips correctly after PUT)
- ☐ SyncTray: chat toast fires on new message; badge count increments; DND suppresses
- ☐ Regression: existing sync/conflict/transfer flows unaffected

**Test command:** `dotnet test`

**Status:** ☐

---

## Step 7 — Phase 2.10: Release Hardening

**Work:**

- ☐ **Accessibility audit:** All interactive elements have `title`, `aria-label`, or `aria-describedby`
- ☐ **Empty state copy review:** Verify channel list, DM view, announcement list, message list empty states have user-friendly text
- ☐ **Error state handling:** Add error display (`ErrorMessage` property + conditional markup) to `ChannelList`, `MessageList`, `AnnouncementList` for API call failures
- ☐ **Loading skeletons:** Add a `IsLoading` skeleton/spinner consistent with the rest of the UI to `ChannelList` and `AnnouncementList`
- ☐ **Settings window DND toggle:** Wire the `IsMuteChatNotifications` setting (Step 5c) to the Settings UI window

**Files:** Various UI components

**Status:** ☐

---

## Execution Order

| Step | Phase | Description | Complexity |
|------|-------|-------------|------------|
| 1 | 2.5 | Realtime badge wiring | Low |
| 2a | 2.6 | Announcement filters | Low |
| 2b | 2.6 | Announcement preview | Low |
| 3a | 2.7 | Push API client + DTOs | Low |
| 3b | 2.7 | Notification preferences UI | Medium |
| 4a | 2.8 | Markdown toolbar | Medium |
| 4b | 2.8 | @mention autocomplete | Medium |
| 4c | 2.8 | Paste image | Medium |
| 4d | 2.8 | Drag-to-reorder | Medium |
| 4e | 2.8 | DM user search | Medium |
| 4f | 2.8 | Group DM | Low |
| 5a | 2.9 | Tray chat badge | Medium |
| 5b | 2.9 | Toast notifications | Medium |
| 5c | 2.9 | DND in tray | Low |
| 5d | 2.9 | Click-to-open browser | Low |
| 6 | 2.9 | Regression pass | Low |
| 7 | 2.10 | Release hardening | Medium |

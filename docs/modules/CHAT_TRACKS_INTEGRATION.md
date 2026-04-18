# Chat ↔ Tracks Real-Time Integration

> **Version:** 0.1.0-alpha  
> **Status:** Implemented  
> **Modules:** `dotnetcloud.chat`, `dotnetcloud.tracks`

---

## Overview

The Chat and Tracks modules maintain a **bidirectional real-time integration** via the DotNetCloud event bus and SignalR. When either module is active, users see live cross-module activity without leaving their current view:

- **Chat sidebar** shows a live **Board Activity** feed of Tracks events (cards created/moved, sprints started, assignments)
- **Tracks board view** shows a compact **Chat Activity** indicator when chat messages or channel events occur

Both directions degrade gracefully — if either module is not installed, the integration UI elements are hidden automatically.

## Architecture

```
┌──────────────────────────────────┐       ┌──────────────────────────────────┐
│         Chat Module              │       │         Tracks Module            │
│                                  │       │                                  │
│  TracksActivityChatHandler       │◄──────│  Card / Sprint / Board events    │
│    ↓ IRealtimeBroadcaster        │       │                                  │
│    ↓ SignalR → tracks-activity   │       │  ChatMessageTracksHandler        │◄──┐
│                                  │       │    ↓ ITracksRealtimeService      │   │
│  TracksActivityFeed (Blazor)     │       │                                  │   │
│    ↑ ITracksActivitySignalRService       │  ChatActivityIndicator (Blazor)  │   │
│                                  │       │    ↑ IChatActivitySignalRService │   │
│  Message / Channel events ───────│───────┘                                  │   │
│                                  │                                          │   │
└──────────────────────────────────┘       └──────────────────────────────────┘   │
         │                                                                        │
         └────────────────────────────────────────────────────────────────────────┘
```

### Event Flow

1. **Tracks → Chat:** When a Tracks event fires (e.g., `CardCreatedEvent`), the `TracksActivityChatHandler` in the Chat module catches it and broadcasts a `TracksActivityNotification` signal via `IRealtimeBroadcaster` to the `tracks-activity` SignalR group.

2. **Chat → Tracks:** When a Chat event fires (e.g., `MessageSentEvent`), the `ChatMessageTracksHandler` in the Tracks module catches it and broadcasts via `ITracksRealtimeService` to connected board views.

3. All connected users are **automatically joined** to the `tracks-activity` group on hub connection — no client-side opt-in required.

## SignalR Events

### Server → Client Events (Tracks Activity in Chat)

| Event Name | Payload | Description |
|---|---|---|
| `TracksActivityNotification` | `{ action, boardId, cardId?, title?, userId }` | General board activity signal |
| `TracksCardAssignedToYou` | `{ cardId, boardId, assignedByUserId }` | Direct notification when you are assigned to a card |

**Broadcast groups:**
- `tracks-activity` — Global group; all connected users receive these signals
- `tracks-board-chat-{boardId}` — Board-scoped group for targeted updates

### Server → Client Events (Chat Activity in Tracks)

Chat activity is broadcast through the Tracks real-time service using the existing Tracks activity infrastructure:

| Activity Action | Entity Type | Description |
|---|---|---|
| `chat_message_sent` | `ChatMessage` | A message was sent in any channel |
| `chat_channel_created` | `ChatChannel` | A new channel was created |
| `chat_channel_deleted` | `ChatChannel` | A channel was deleted |

### Client → Server Hub Methods

| Method | Parameters | Description |
|---|---|---|
| `JoinBoardChatGroupAsync` | `boardId: string` | Subscribe to board-specific Tracks ↔ Chat events |
| `LeaveBoardChatGroupAsync` | `boardId: string` | Unsubscribe from a board-specific group |

## Tracked Events

### Tracks Events Consumed by Chat

The Chat module subscribes to 10 Tracks events:

| Event | Action | Notification |
|---|---|---|
| `CardCreatedEvent` | `card_created` | Group broadcast |
| `CardMovedEvent` | `card_moved` | Group broadcast |
| `CardUpdatedEvent` | `card_updated` | Group broadcast |
| `CardDeletedEvent` | `card_deleted` | Group broadcast |
| `CardAssignedEvent` | `card_assigned` | Group broadcast + direct user notification |
| `CardCommentAddedEvent` | `comment_added` | Group broadcast |
| `SprintStartedEvent` | `sprint_started` | Group broadcast |
| `SprintCompletedEvent` | `sprint_completed` | Group broadcast |
| `BoardCreatedEvent` | `board_created` | Group broadcast |
| `BoardDeletedEvent` | `board_deleted` | Group broadcast |

### Chat Events Consumed by Tracks

The Tracks module subscribes to 3 Chat events:

| Event | Action | Scope |
|---|---|---|
| `MessageSentEvent` | `chat_message_sent` | Global (all boards) |
| `ChannelCreatedEvent` | `chat_channel_created` | Global |
| `ChannelDeletedEvent` | `chat_channel_deleted` | Global |

## UI Components

### TracksActivityFeed (Chat Sidebar)

**Location:** `DotNetCloud.Modules.Chat/UI/TracksActivityFeed.razor`

A sidebar widget in the Chat interface that shows a live stream of board activity:

- Displays up to 20 most recent activities (newest first)
- Each item shows an action icon, descriptive text, and relative timestamp
- New items animate in with a slide-down effect
- Card assignment alerts appear as a dismissible blue banner
- Hidden entirely when Tracks module is not installed

**Action Icons:**

| Action | Icon |
|---|---|
| Card created | 📋 |
| Card moved | ➡ |
| Card updated | ✏ |
| Card deleted | 🗑 |
| Card assigned | 👤 |
| Comment added | 💬 |
| Sprint started | 🏃 |
| Sprint completed | ✅ |
| Board created | 📋 |
| Board deleted | 🗑 |

### ChatActivityIndicator (Tracks Board View)

**Location:** `DotNetCloud.Modules.Tracks/UI/ChatActivityIndicator.razor`

A compact widget in the Tracks board view showing chat activity:

- Displays a toast notification when a new chat message is sent
- Shows channel created/deleted events with a dismiss button
- Pulsing green "Chat active" status dot when the Chat module is connected
- Hidden entirely when Chat module is not installed

## Graceful Degradation (Optional Modules)

Both modules use the **null-object pattern** for optional dependencies:

| Module | Service Interface | Null Stub | Behavior When Missing |
|---|---|---|---|
| Chat | `ITracksActivitySignalRService` | `NullTracksActivitySignalRService` | `IsActive = false`, events never fire, UI hidden |
| Tracks | `IChatActivitySignalRService` | `NullChatActivitySignalRService` | `IsActive = false`, events never fire, UI hidden |

The null stubs are registered by default in each module's DI container. When the counterpart module is present and connected, the live implementation replaces the stub.

**Component behavior:**
- Both components check `IsActive` on their injected service
- If `false`, the component returns immediately without rendering
- No errors, no empty containers — completely invisible

## Source Files

### Chat Module (Tracks integration)

| File | Purpose |
|---|---|
| `Events/TracksActivityChatHandler.cs` | Event handler subscribing to 10 Tracks events |
| `Services/TracksActivitySignalRService.cs` | `ITracksActivitySignalRService` interface + null stub |
| `UI/TracksActivityFeed.razor` | Blazor template |
| `UI/TracksActivityFeed.razor.cs` | Component code-behind |
| `UI/TracksActivityFeed.razor.css` | Scoped styles |

### Tracks Module (Chat integration)

| File | Purpose |
|---|---|
| `Events/ChatMessageTracksHandler.cs` | Event handler subscribing to 3 Chat events |
| `Services/ChatActivitySignalRService.cs` | `IChatActivitySignalRService` interface + null stub |
| `UI/ChatActivityIndicator.razor` | Blazor template |
| `UI/ChatActivityIndicator.razor.cs` | Component code-behind |
| `UI/ChatActivityIndicator.razor.css` | Scoped styles |

### Core Server

| File | Purpose |
|---|---|
| `RealTime/CoreHub.cs` | Auto-joins `tracks-activity` group; `JoinBoardChatGroupAsync`/`LeaveBoardChatGroupAsync` methods |

## Client Integration Guide

### Subscribing to Tracks Activity in Chat

```javascript
// SignalR client (JS/TS)
connection.on("TracksActivityNotification", (data) => {
    // data: { action, boardId, cardId, title, userId, ... }
    console.log(`Board activity: ${data.action}`);
});

connection.on("TracksCardAssignedToYou", (data) => {
    // data: { cardId, boardId, assignedByUserId }
    showAssignmentAlert(data);
});
```

### Subscribing to Board-Scoped Events

```javascript
// Join a board's chat integration group
await connection.invoke("JoinBoardChatGroupAsync", boardId);

// Leave when navigating away
await connection.invoke("LeaveBoardChatGroupAsync", boardId);
```

### Mobile Clients (C#)

```csharp
_hubConnection.On<TracksActivitySignal>("TracksActivityNotification", signal =>
{
    // Update activity feed UI
    ActivityFeed.Insert(0, signal);
});

_hubConnection.On<CardAssignmentNotification>("TracksCardAssignedToYou", notification =>
{
    // Show push notification or in-app alert
    NotificationService.ShowAssignment(notification);
});
```

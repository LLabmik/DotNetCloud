# Chat Real-Time Events (SignalR)

## Overview

The Chat module uses DotNetCloud's SignalR hub (`CoreHub`) for real-time communication. Chat events are broadcast through the `IRealtimeBroadcaster` capability interface, which the core server provides to process-isolated modules. Clients connect to the hub and receive events without polling.

## Connection

### Hub Endpoint

```
wss://{server}/hubs/core
```

### Authentication

Bearer token authentication via query string:

```
wss://{server}/hubs/core?access_token={jwt}
```

### Auto-Reconnect

Clients should implement automatic reconnection with exponential backoff. The SignalR client library provides built-in retry policies.

## Client → Server Methods

Methods the client can invoke on the hub:

### SendMessageAsync

Send a message to a channel. Returns the created message.

```
Hub.InvokeAsync<MessageDto>("SendMessageAsync", channelId, content, replyToId?)
```

| Parameter | Type | Description |
|---|---|---|
| `channelId` | `Guid` | Target channel |
| `content` | `string` | Message content (Markdown) |
| `replyToId` | `Guid?` | Parent message ID for threading |

**Returns:** `MessageDto`

---

### EditMessageAsync

Edit an existing message. Returns the updated message.

```
Hub.InvokeAsync<MessageDto>("EditMessageAsync", messageId, newContent)
```

| Parameter | Type | Description |
|---|---|---|
| `messageId` | `Guid` | Message to edit |
| `newContent` | `string` | New content |

**Returns:** `MessageDto`

---

### DeleteMessageAsync

Delete a message from a channel.

```
Hub.InvokeAsync("DeleteMessageAsync", messageId)
```

---

### StartTypingAsync

Signal that the user started typing in a channel.

```
Hub.InvokeAsync("StartTypingAsync", channelId, displayName?)
```

| Parameter | Type | Description |
|---|---|---|
| `channelId` | `Guid` | Channel where user is typing |
| `displayName` | `string?` | User display name |

---

### StopTypingAsync

Signal that the user stopped typing.

```
Hub.InvokeAsync("StopTypingAsync", channelId)
```

---

### MarkReadAsync

Mark a channel as read up to a specific message. Triggers an unread count update broadcast.

```
Hub.InvokeAsync("MarkReadAsync", channelId, messageId)
```

---

### AddReactionAsync

Add an emoji reaction to a message. Broadcasts the updated reaction list.

```
Hub.InvokeAsync("AddReactionAsync", messageId, emoji)
```

---

### RemoveReactionAsync

Remove an emoji reaction from a message.

```
Hub.InvokeAsync("RemoveReactionAsync", messageId, emoji)
```

---

### SetPresenceAsync

Update the user's presence status and optional status message.

```
Hub.InvokeAsync<PresenceDto>("SetPresenceAsync", status, statusMessage?)
```

| Parameter | Type | Description |
|---|---|---|
| `status` | `string` | `Online`, `Away`, `DoNotDisturb`, `Offline` |
| `statusMessage` | `string?` | Custom status text |

**Returns:** `PresenceDto`

---

### JoinGroupAsync / LeaveGroupAsync

Join or leave a broadcast group. Used for channel-scoped event delivery.

```
Hub.InvokeAsync("JoinGroupAsync", groupName)
Hub.InvokeAsync("LeaveGroupAsync", groupName)
```

Group naming convention: `chat-channel-{channelId}`

---

### PingAsync

Keep-alive ping to maintain connection and update presence.

```
Hub.InvokeAsync("PingAsync")
```

---

## Server → Client Events

Events the server pushes to connected clients. Register handlers with `Hub.On<T>(...)`.

### NewMessage

Broadcast when a new message is sent in a channel.

```
Hub.On<Guid, MessageDto>("NewMessage", (channelId, message) => { ... })
```

**Scope:** All members of the channel group (`chat-channel-{channelId}`)

---

### MessageEdited

Broadcast when a message is edited.

```
Hub.On<Guid, MessageDto>("MessageEdited", (channelId, message) => { ... })
```

---

### MessageDeleted

Broadcast when a message is deleted.

```
Hub.On<Guid, Guid>("MessageDeleted", (channelId, messageId) => { ... })
```

---

### TypingStarted

Broadcast when a user starts typing.

```
Hub.On<Guid, Guid, string?>("TypingStarted", (channelId, userId, displayName) => { ... })
```

---

### ReactionUpdated

Broadcast when reactions on a message change (add or remove).

```
Hub.On<Guid, Guid, MessageReactionDto[]>("ReactionUpdated", (channelId, messageId, reactions) => { ... })
```

---

### ChannelUpdated

Broadcast when a channel's metadata changes (name, topic, description, archive status).

```
Hub.On<ChannelDto>("ChannelUpdated", (channel) => { ... })
```

---

### MemberJoined

Broadcast when a user joins a channel.

```
Hub.On<Guid, ChannelMemberDto>("MemberJoined", (channelId, member) => { ... })
```

---

### MemberLeft

Broadcast when a user leaves or is removed from a channel.

```
Hub.On<Guid, Guid>("MemberLeft", (channelId, userId) => { ... })
```

---

### UnreadCountUpdated

Sent to a specific user when their unread count changes for a channel.

```
Hub.On<Guid, int>("UnreadCountUpdated", (channelId, count) => { ... })
```

**Scope:** Targeted to the specific user (via `SendToUserAsync`)

---

### PresenceChanged

Broadcast when a user's presence status changes.

```
Hub.On<PresenceDto>("PresenceChanged", (presence) => { ... })
```

**Payload:**

| Field | Type | Description |
|---|---|---|
| `userId` | `Guid` | User whose presence changed |
| `status` | `string` | `Online`, `Away`, `DoNotDisturb`, `Offline` |
| `statusMessage` | `string?` | Custom status text |
| `lastSeenAt` | `DateTime?` | Last activity timestamp |

---

### AnnouncementCreated

Broadcast when a new announcement is published.

```
Hub.On<AnnouncementDto>("AnnouncementCreated", (announcement) => { ... })
```

**Scope:** All connected users

---

### UrgentAnnouncement

Broadcast when an announcement with `Urgent` priority is published.

```
Hub.On<AnnouncementDto>("UrgentAnnouncement", (announcement) => { ... })
```

**Scope:** All connected users

---

### AnnouncementBadgeUpdated

Broadcast when the unacknowledged announcement count changes for a user.

```
Hub.On<int>("AnnouncementBadgeUpdated", (badgeCount) => { ... })
```

---

## Domain Events (IEventBus)

These events are published on the internal event bus for cross-module communication. They implement `IEvent` with `EventId` (Guid) and `CreatedAt` (DateTime).

| Event | Key Properties | Published When |
|---|---|---|
| `MessageSentEvent` | `MessageId`, `ChannelId`, `SenderUserId`, `Content`, `MessageType` | Message sent |
| `MessageEditedEvent` | `MessageId`, `ChannelId`, `EditedByUserId`, `NewContent` | Message edited |
| `MessageDeletedEvent` | `MessageId`, `ChannelId`, `DeletedByUserId` | Message deleted |
| `ChannelCreatedEvent` | `ChannelId`, `ChannelName`, `ChannelType`, `CreatedByUserId` | Channel created |
| `ChannelDeletedEvent` | `ChannelId`, `ChannelName`, `DeletedByUserId` | Channel deleted |
| `ChannelArchivedEvent` | `ChannelId`, `ChannelName`, `ArchivedByUserId` | Channel archived |
| `UserJoinedChannelEvent` | `ChannelId`, `UserId`, `AddedByUserId` | User added to channel |
| `UserLeftChannelEvent` | `ChannelId`, `UserId`, `RemovedByUserId` | User removed from channel |
| `ReactionAddedEvent` | `MessageId`, `ChannelId`, `UserId`, `Emoji` | Reaction added |
| `ReactionRemovedEvent` | `MessageId`, `ChannelId`, `UserId`, `Emoji` | Reaction removed |
| `PresenceChangedEvent` | `UserId`, `Status`, `StatusMessage`, `LastSeenAt` | Presence updated |

### Subscribed Events

The Chat module subscribes to events from other modules:

| Event | Source | Purpose |
|---|---|---|
| `FileUploadedEvent` | Files module | File sharing integration — enables file attachments in chat |

## Connection Lifecycle

### On Connect

1. User is authenticated via bearer token.
2. User is added to their channel groups (`chat-channel-{channelId}` for each membership).
3. Presence is set to `Online`.
4. `PresenceChanged` event is broadcast to relevant channels.

### On Disconnect

1. User is removed from all channel groups.
2. Presence is set to `Offline`.
3. `PresenceChanged` event is broadcast.

### Reconnection

On reconnect, the full `OnConnectedAsync` flow runs again — the client is re-added to all channel groups and presence is restored. Clients should re-fetch messages since last known timestamp to catch any missed during disconnection.

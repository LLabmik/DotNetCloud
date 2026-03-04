# Chat Module Architecture

## Overview

The Chat module (`DotNetCloud.Modules.Chat`) provides real-time messaging, channels, direct messages, reactions, pins, typing indicators, file sharing, announcements, and push notifications for the DotNetCloud platform.

## Project Structure

```
src/Modules/Chat/
├── DotNetCloud.Modules.Chat/           # Core: models, DTOs, events, service interfaces, UI
│   ├── Models/                          # EF Core entities
│   ├── DTOs/                            # Data transfer objects
│   ├── Events/                          # Domain events + handlers
│   ├── Services/                        # Service interfaces + implementations
│   └── UI/                              # Blazor components + view models
├── DotNetCloud.Modules.Chat.Data/       # Data: DbContext, EF configs, service impls
│   ├── Configuration/                   # EF Core entity configurations
│   └── Services/                        # Service implementations (ChannelService, etc.)
└── DotNetCloud.Modules.Chat.Host/       # Host: REST API controllers, gRPC, health checks
    ├── Controllers/                     # ChatController, AnnouncementController
    ├── Services/                        # gRPC services, health check, InProcessEventBus
    └── Protos/                          # gRPC proto definitions
```

## Key Components

### Models

| Entity | Purpose |
|--------|---------|
| `Channel` | Chat channel (public, private, DM, group) |
| `ChannelMember` | User membership in a channel with role and preferences |
| `Message` | Chat message with content, type, reply threading |
| `MessageAttachment` | File attached to a message |
| `MessageReaction` | Emoji reaction on a message |
| `MessageMention` | @mention in a message |
| `PinnedMessage` | Message pinned in a channel |
| `Announcement` | Organization-wide announcement |
| `AnnouncementAcknowledgement` | User acknowledgement of an announcement |

### Service Layer

| Service | Responsibility |
|---------|---------------|
| `IChannelService` | Channel CRUD, archive, DM creation |
| `IChannelMemberService` | Member management, roles, notifications, read tracking |
| `IMessageService` | Send, edit, delete, search, paginated retrieval |
| `IReactionService` | Add/remove emoji reactions |
| `IPinService` | Pin/unpin messages |
| `ITypingIndicatorService` | Track who is typing |
| `IAnnouncementService` | Announcement CRUD with acknowledgement |
| `IChatRealtimeService` | SignalR broadcast wrapper |
| `IPushNotificationService` | Push notifications via FCM/UnifiedPush |

### Real-Time Architecture

```
Client (Blazor/MAUI)
  ↕ SignalR WebSocket
Core Server (CoreHub)
  ↕ IRealtimeBroadcaster capability
Chat Module (ChatRealtimeService)
  → Broadcasts: NewMessage, MessageEdited, MessageDeleted,
    TypingIndicator, ReactionUpdated, ChannelUpdated,
    MemberJoined, MemberLeft, UnreadCountUpdated, PresenceChanged
```

### Push Notification Flow

```
Chat Service → NotificationRouter
                ├── FcmPushProvider (Firebase Cloud Messaging)
                └── UnifiedPushProvider (open protocol)
```

## Blazor UI Components

| Component | Purpose |
|-----------|---------|
| `ChannelList` | Sidebar channel navigation with unread badges |
| `ChannelHeader` | Channel name, topic, member count, actions |
| `MessageList` | Scrollable message list with infinite scroll |
| `MessageComposer` | Message input with reply, attach, emoji |
| `TypingIndicator` | Animated typing dots with user names |
| `MemberListPanel` | Channel members grouped by status |
| `ChannelSettingsDialog` | Edit channel metadata, notifications, archive/delete |
| `DirectMessageView` | Streamlined 1:1 conversation view |
| `ChatNotificationBadge` | Unread count badge for navigation |
| `AnnouncementBanner` | Inline urgent/important announcement display |
| `AnnouncementList` | Full announcement listing with filters |
| `AnnouncementEditor` | Create/edit announcement dialog |

## REST API

Base path: `/api/v1/chat`

### Channels
- `POST /channels` — Create channel
- `GET /channels` — List user's channels
- `GET /channels/{id}` — Get channel
- `PUT /channels/{id}` — Update channel
- `DELETE /channels/{id}` — Delete channel (soft)
- `POST /channels/{id}/archive` — Archive channel
- `POST /channels/dm/{userId}` — Get or create DM

### Members
- `POST /channels/{id}/members` — Add member
- `DELETE /channels/{id}/members/{userId}` — Remove member
- `GET /channels/{id}/members` — List members
- `PUT /channels/{id}/members/{userId}/role` — Update role
- `PUT /channels/{id}/notifications` — Update notification preference
- `POST /channels/{id}/read` — Mark as read
- `GET /unread` — Get unread counts

### Messages
- `POST /channels/{id}/messages` — Send message
- `GET /channels/{id}/messages` — Get messages (paginated)
- `GET /channels/{id}/messages/{msgId}` — Get single message
- `PUT /channels/{id}/messages/{msgId}` — Edit message
- `DELETE /channels/{id}/messages/{msgId}` — Delete message
- `GET /channels/{id}/messages/search?q=` — Search messages

### Reactions
- `POST /messages/{id}/reactions` — Add reaction
- `DELETE /messages/{id}/reactions/{emoji}` — Remove reaction
- `GET /messages/{id}/reactions` — Get reactions

### Pins
- `POST /channels/{id}/pins/{msgId}` — Pin message
- `DELETE /channels/{id}/pins/{msgId}` — Unpin message
- `GET /channels/{id}/pins` — Get pinned messages

### Typing
- `POST /channels/{id}/typing` — Notify typing
- `GET /channels/{id}/typing` — Get typing users

### Announcements (`/api/v1/announcements`)
- `POST /` — Create announcement
- `GET /` — List announcements
- `GET /{id}` — Get announcement
- `PUT /{id}` — Update announcement
- `DELETE /{id}` — Delete announcement
- `POST /{id}/acknowledge` — Acknowledge
- `GET /{id}/acknowledgements` — List acknowledgements

## Testing

All service tests are in `tests/DotNetCloud.Modules.Chat.Tests/`:

| Test Class | Coverage |
|-----------|---------|
| `ChannelServiceTests` | Channel CRUD, DM, archive, validation |
| `MessageServiceTests` | Send, edit, delete, search, pagination, auth |
| `ReactionServiceTests` | Add/remove, duplicates, grouping, validation |
| `PinServiceTests` | Pin/unpin, duplicates, empty state |
| `TypingIndicatorServiceTests` | Notify, cleanup, multi-user, isolation |

Run tests:
```bash
dotnet test tests/DotNetCloud.Modules.Chat.Tests/
```

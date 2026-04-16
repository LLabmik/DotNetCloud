# Chat Module REST API Reference

> **Base URL:** `/api/v1/chat/` (channels, messages, reactions, pins, typing, video calls)
> **Announcements:** `/api/v1/announcements/`
> **Notifications:** `/api/v1/notifications/`
> **Authentication:** All endpoints require a `userId` query parameter identifying the caller.

All responses use the standard DotNetCloud API envelope format. See [API Response Format](../../api/RESPONSE_FORMAT.md).

---

## Channel Endpoints

### Create Channel

```
POST /api/v1/chat/channels?userId={userId}
```

**Request Body:**
```json
{
  "name": "general",
  "description": "General discussion",
  "type": "Public",
  "topic": "Welcome to the team",
  "organizationId": "00000000-0000-0000-0000-000000000000",
  "memberIds": ["guid1", "guid2"]
}
```

**Response:** `201 Created` → `ChannelDto`

| Field | Type | Description |
|---|---|---|
| `id` | `Guid` | Channel identifier |
| `name` | `string` | Channel name |
| `description` | `string?` | Channel description |
| `type` | `string` | `Public`, `Private`, `DirectMessage`, `Group` |
| `topic` | `string?` | Current topic |
| `avatarUrl` | `string?` | Channel avatar URL |
| `isArchived` | `bool` | Whether the channel is archived |
| `memberCount` | `int` | Number of members |
| `lastActivityAt` | `DateTime?` | Last message timestamp |
| `createdAt` | `DateTime` | Creation timestamp |
| `createdByUserId` | `Guid` | Creator user ID |

**Errors:**
- `400` — Missing or invalid fields
- `409` — Channel name already exists

---

### List Channels

```
GET /api/v1/chat/channels?userId={userId}
```

**Response:** `200 OK` → `ChannelDto[]`

---

### Get Channel

```
GET /api/v1/chat/channels/{channelId}?userId={userId}
```

**Response:** `200 OK` → `ChannelDto`

**Errors:**
- `404` — Channel not found

---

### Update Channel

```
PUT /api/v1/chat/channels/{channelId}?userId={userId}
```

**Request Body:**
```json
{
  "name": "renamed-channel",
  "description": "Updated description",
  "topic": "New topic"
}
```

All fields are optional — only provided fields are updated.

**Response:** `200 OK` → `ChannelDto`

---

### Delete Channel

```
DELETE /api/v1/chat/channels/{channelId}?userId={userId}
```

**Response:** `200 OK` → `{ "deleted": true }`

---

### Archive Channel

```
POST /api/v1/chat/channels/{channelId}/archive?userId={userId}
```

**Response:** `200 OK` → `{ "archived": true }`

---

### Get or Create Direct Message

```
POST /api/v1/chat/channels/dm/{otherUserId}?userId={userId}
```

Returns existing DM channel if one exists, or creates a new one.

**Response:** `200 OK` → `ChannelDto`

---

## Member Endpoints

### Add Member

```
POST /api/v1/chat/channels/{channelId}/members?userId={userId}
```

**Request Body:**
```json
{
  "userId": "target-user-guid",
  "role": "Member"
}
```

**Response:** `200 OK` → `{ "added": true }`

**Errors:**
- `403` — Caller lacks permission to add members

---

### Remove Member

```
DELETE /api/v1/chat/channels/{channelId}/members/{targetUserId}?userId={userId}
```

**Response:** `200 OK` → `{ "removed": true }`

**Errors:**
- `403` — Cannot remove last owner

---

### List Members

```
GET /api/v1/chat/channels/{channelId}/members?userId={userId}
```

**Response:** `200 OK` → `ChannelMemberDto[]`

| Field | Type | Description |
|---|---|---|
| `userId` | `Guid` | Member user ID |
| `role` | `string` | `Member`, `Admin`, `Owner` |
| `joinedAt` | `DateTime` | When the user joined |
| `isMuted` | `bool` | Whether channel is muted |
| `notificationPref` | `string` | `All`, `Mentions`, `None` |

---

### Update Member Role

```
PUT /api/v1/chat/channels/{channelId}/members/{targetUserId}/role?userId={userId}
```

**Request Body:**
```json
{
  "role": "Admin"
}
```

**Response:** `200 OK` → `{ "updated": true }`

---

### Update Notification Preference

```
PUT /api/v1/chat/channels/{channelId}/notifications?userId={userId}
```

**Request Body:**
```json
{
  "preference": "Mentions"
}
```

**Response:** `200 OK` → `{ "updated": true }`

---

### Mark Channel as Read

```
POST /api/v1/chat/channels/{channelId}/read?userId={userId}
```

**Request Body:**
```json
{
  "messageId": "last-read-message-guid"
}
```

**Response:** `200 OK` → `{ "marked": true }`

---

### Get Unread Counts

```
GET /api/v1/chat/unread?userId={userId}
```

**Response:** `200 OK` → `UnreadCountDto[]`

| Field | Type | Description |
|---|---|---|
| `channelId` | `Guid` | Channel identifier |
| `unreadCount` | `int` | Number of unread messages |
| `mentionCount` | `int` | Number of unread @mentions |

---

## Message Endpoints

### Send Message

```
POST /api/v1/chat/channels/{channelId}/messages?userId={userId}
```

**Request Body:**
```json
{
  "content": "Hello, world! @alice check this out",
  "replyToMessageId": null
}
```

Content supports Markdown formatting. @mentions are parsed automatically.

**Response:** `201 Created` → `MessageDto`

| Field | Type | Description |
|---|---|---|
| `id` | `Guid` | Message identifier |
| `channelId` | `Guid` | Parent channel |
| `senderUserId` | `Guid` | Author user ID |
| `content` | `string` | Markdown content |
| `type` | `string` | `Text`, `System`, `Notification` |
| `sentAt` | `DateTime` | Send timestamp |
| `editedAt` | `DateTime?` | Last edit timestamp |
| `isEdited` | `bool` | Whether message was edited |
| `replyToMessageId` | `Guid?` | Parent message for threads |
| `attachments` | `MessageAttachmentDto[]` | File attachments |
| `reactions` | `MessageReactionDto[]` | Emoji reactions |
| `mentions` | `MessageMentionDto[]` | Parsed @mentions |

---

### List Messages (Paginated)

```
GET /api/v1/chat/channels/{channelId}/messages?userId={userId}&page=1&pageSize=50
```

**Response:** `200 OK` → Paginated result

| Field | Type | Description |
|---|---|---|
| `items` | `MessageDto[]` | Messages for current page |
| `page` | `int` | Current page number |
| `pageSize` | `int` | Items per page |
| `totalItems` | `int` | Total message count |
| `totalPages` | `int` | Total page count |

---

### Get Message

```
GET /api/v1/chat/channels/{channelId}/messages/{messageId}?userId={userId}
```

**Response:** `200 OK` → `MessageDto`

---

### Edit Message

```
PUT /api/v1/chat/channels/{channelId}/messages/{messageId}?userId={userId}
```

**Request Body:**
```json
{
  "content": "Updated message content"
}
```

**Response:** `200 OK` → `MessageDto` (with `isEdited: true`)

---

### Delete Message

```
DELETE /api/v1/chat/channels/{channelId}/messages/{messageId}?userId={userId}
```

**Response:** `200 OK` → `{ "deleted": true }`

**Errors:**
- `404` — Message not found

---

### Search Messages

```
GET /api/v1/chat/channels/{channelId}/messages/search?q=keyword&userId={userId}&page=1&pageSize=50
```

**Query Parameters:**

| Parameter | Required | Description |
|---|---|---|
| `q` | Yes | Search query (cannot be empty) |
| `page` | No | Page number (default: 1) |
| `pageSize` | No | Items per page (default: 50) |

**Response:** `200 OK` → Paginated `MessageDto[]`

**Errors:**
- `400` — Empty search query

---

### List Channel Files

```
GET /api/v1/chat/channels/{channelId}/files?userId={userId}
```

**Response:** `200 OK` → `MessageAttachmentDto[]`

---

## Reaction Endpoints

### Add Reaction

```
POST /api/v1/chat/messages/{messageId}/reactions?userId={userId}
```

**Request Body:**
```json
{
  "emoji": "👍"
}
```

**Response:** `200 OK` → `{ "added": true }`

---

### Remove Reaction

```
DELETE /api/v1/chat/messages/{messageId}/reactions/{emoji}?userId={userId}
```

**Response:** `200 OK` → `{ "removed": true }`

---

### Get Reactions

```
GET /api/v1/chat/messages/{messageId}/reactions
```

**Response:** `200 OK` → `MessageReactionDto[]`

| Field | Type | Description |
|---|---|---|
| `emoji` | `string` | Reaction emoji |
| `count` | `int` | Number of users who reacted |
| `userIds` | `Guid[]` | Users who added this reaction |

---

## Pin Endpoints

### Pin Message

```
POST /api/v1/chat/channels/{channelId}/pins/{messageId}?userId={userId}
```

**Response:** `200 OK` → `{ "pinned": true }`

---

### Unpin Message

```
DELETE /api/v1/chat/channels/{channelId}/pins/{messageId}?userId={userId}
```

**Response:** `200 OK` → `{ "unpinned": true }`

---

### List Pinned Messages

```
GET /api/v1/chat/channels/{channelId}/pins?userId={userId}
```

**Response:** `200 OK` → `MessageDto[]` (ordered by `PinnedAt`)

---

## Typing Indicator Endpoints

### Notify Typing

```
POST /api/v1/chat/channels/{channelId}/typing?userId={userId}
```

**Response:** `200 OK` → `{ "typing": true }`

---

### Get Typing Users

```
GET /api/v1/chat/channels/{channelId}/typing
```

**Response:** `200 OK` → `TypingIndicatorDto[]`

| Field | Type | Description |
|---|---|---|
| `channelId` | `Guid` | Channel identifier |
| `userId` | `Guid` | User who is typing |
| `displayName` | `string?` | User display name |

---

## Attachment Endpoints

### Add Attachment

```
POST /api/v1/chat/channels/{channelId}/messages/{messageId}/attachments?userId={userId}
```

**Request Body:**
```json
{
  "fileName": "report.pdf",
  "mimeType": "application/pdf",
  "fileSize": 1048576,
  "thumbnailUrl": null,
  "fileNodeId": "files-module-guid"
}
```

**Response:** `201 Created` → `MessageAttachmentDto`

| Field | Type | Description |
|---|---|---|
| `id` | `Guid` | Attachment identifier |
| `fileName` | `string` | Original file name |
| `mimeType` | `string` | MIME type |
| `fileSize` | `long` | Size in bytes |
| `thumbnailUrl` | `string?` | Thumbnail URL (for images) |
| `fileNodeId` | `Guid?` | Link to Files module `FileNode` |

---

## Announcement Endpoints

### Create Announcement

```
POST /api/v1/announcements?userId={userId}
```

**Request Body:**
```json
{
  "organizationId": "org-guid",
  "title": "System Maintenance",
  "content": "Scheduled maintenance this weekend.",
  "priority": "Important",
  "expiresAt": "2026-04-01T00:00:00Z",
  "requiresAcknowledgement": true
}
```

**Response:** `201 Created` → `AnnouncementDto`

| Field | Type | Description |
|---|---|---|
| `id` | `Guid` | Announcement identifier |
| `organizationId` | `Guid` | Organization scope |
| `authorUserId` | `Guid` | Author user ID |
| `title` | `string` | Announcement title |
| `content` | `string` | Markdown content |
| `priority` | `string` | `Normal`, `Important`, `Urgent` |
| `publishedAt` | `DateTime` | Publication timestamp |
| `expiresAt` | `DateTime?` | Expiration timestamp |
| `isPinned` | `bool` | Whether pinned to top |
| `requiresAcknowledgement` | `bool` | Whether users must acknowledge |
| `acknowledgementCount` | `int` | Number of acknowledgements |

---

### List Announcements

```
GET /api/v1/announcements?userId={userId}
```

**Response:** `200 OK` → `AnnouncementDto[]`

---

### Get Announcement

```
GET /api/v1/announcements/{id}?userId={userId}
```

**Response:** `200 OK` → `AnnouncementDto`

**Errors:**
- `404` — Announcement not found

---

### Update Announcement

```
PUT /api/v1/announcements/{id}?userId={userId}
```

**Request Body:**
```json
{
  "title": "Updated Title",
  "content": "Updated content",
  "priority": "Urgent",
  "expiresAt": "2026-05-01T00:00:00Z",
  "isPinned": true
}
```

All fields are optional.

**Response:** `200 OK` → `{ "updated": true }`

---

### Delete Announcement

```
DELETE /api/v1/announcements/{id}?userId={userId}
```

**Response:** `200 OK` → `{ "deleted": true }`

---

### Acknowledge Announcement

```
POST /api/v1/announcements/{id}/acknowledge?userId={userId}
```

**Response:** `200 OK` → `{ "acknowledged": true }`

---

### Get Acknowledgements

```
GET /api/v1/announcements/{id}/acknowledgements?userId={userId}
```

**Response:** `200 OK` → `AnnouncementAcknowledgementDto[]`

| Field | Type | Description |
|---|---|---|
| `userId` | `Guid` | User who acknowledged |
| `acknowledgedAt` | `DateTime` | Acknowledgement timestamp |

---

## Push Notification Endpoints

### Register Device

```
POST /api/v1/notifications/devices/register?userId={userId}
```

**Request Body:**
```json
{
  "deviceToken": "fcm-or-unified-push-token",
  "provider": "FCM",
  "endpoint": null
}
```

| Field | Required | Description |
|---|---|---|
| `deviceToken` | Yes | FCM token or UnifiedPush endpoint identifier |
| `provider` | Yes | `FCM` or `UnifiedPush` |
| `endpoint` | No | UnifiedPush distributor endpoint URL (required for UnifiedPush) |

**Response:** `200 OK` → `{ "registered": true }`

**Errors:**
- `400` — Empty device token or invalid provider

---

### Unregister Device

```
DELETE /api/v1/notifications/devices/{deviceToken}?userId={userId}
```

**Response:** `200 OK` → `{ "unregistered": true }`

---

### Get Notification Preferences

```
GET /api/v1/notifications/preferences?userId={userId}
```

**Response:** `200 OK` → `NotificationPreferencesDto`

| Field | Type | Description |
|---|---|---|
| `pushEnabled` | `bool` | Whether push notifications are enabled |
| `doNotDisturb` | `bool` | Whether DND mode is active |
| `mutedChannelIds` | `Guid[]` | Channels with suppressed notifications |

---

### Update Notification Preferences

```
PUT /api/v1/notifications/preferences?userId={userId}
```

**Request Body:**
```json
{
  "pushEnabled": true,
  "doNotDisturb": false,
  "mutedChannelIds": []
}
```

**Response:** `200 OK` → `{ "updated": true }`

---

## Video Call Endpoints

### Initiate Call

```
POST /api/v1/chat/channels/{channelId}/calls?userId={userId}
```

**Request Body:**
```json
{
  "mediaType": "Video"
}
```

| Field | Type | Description |
|---|---|---|
| `mediaType` | `string` | `Audio`, `Video`, or `ScreenShare` |

**Response:** `201 Created` → `VideoCallDto`

| Field | Type | Description |
|---|---|---|
| `id` | `Guid` | Call identifier |
| `channelId` | `Guid` | Channel where the call was initiated |
| `initiatorUserId` | `Guid` | User who started the call |
| `state` | `string` | Current state (e.g., `Ringing`) |
| `mediaType` | `string` | `Audio`, `Video`, or `ScreenShare` |
| `isGroupCall` | `bool` | Whether the channel has 3+ members |
| `maxParticipants` | `int` | Peak participant count |
| `participants` | `CallParticipantDto[]` | Current participants |
| `startedAtUtc` | `DateTime?` | When the call became active |
| `endedAtUtc` | `DateTime?` | When the call ended |
| `endReason` | `string?` | End reason (if ended) |
| `liveKitRoomId` | `string?` | LiveKit room name (if SFU is active) |
| `createdAtUtc` | `DateTime` | Creation timestamp |

**Errors:**
- `400` — Invalid media type
- `403` — Not a channel member
- `409` — A call is already active in this channel

**Rate Limit:** `module-video-call-initiate`

---

### Join Call

```
POST /api/v1/chat/calls/{callId}/join?userId={userId}
```

**Request Body:**
```json
{
  "withAudio": true,
  "withVideo": true
}
```

**Response:** `200 OK` → `VideoCallDto` (updated state and participant list)

**Errors:**
- `400` — Invalid request
- `403` — Not a channel member
- `404` — Call not found or not joinable

---

### Leave Call

```
POST /api/v1/chat/calls/{callId}/leave?userId={userId}
```

**Response:** `200 OK` → `{ "left": true }`

**Errors:**
- `403` — Not authorized
- `404` — Call not found

---

### End Call

```
POST /api/v1/chat/calls/{callId}/end?userId={userId}
```

Ends the call for all participants.

**Response:** `200 OK` → `{ "ended": true }`

**Errors:**
- `403` — Not authorized
- `404` — Call not found

---

### Reject Call

```
POST /api/v1/chat/calls/{callId}/reject?userId={userId}
```

**Response:** `200 OK` → `{ "rejected": true }`

**Errors:**
- `403` — Not authorized
- `404` — Call not found

---

### Get Call History

```
GET /api/v1/chat/channels/{channelId}/calls?userId={userId}&skip=0&take=20
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `skip` | `int` | `0` | Number of records to skip |
| `take` | `int` | `20` | Number of records to return (max 100) |

**Response:** `200 OK` → `CallHistoryDto[]`

| Field | Type | Description |
|---|---|---|
| `id` | `Guid` | Call identifier |
| `channelId` | `Guid` | Channel ID |
| `initiatorUserId` | `Guid` | Who started the call |
| `state` | `string` | Final state (`Ended`, `Missed`, `Rejected`, `Failed`) |
| `mediaType` | `string` | Media type |
| `startedAtUtc` | `DateTime?` | When the call started |
| `endedAtUtc` | `DateTime?` | When the call ended |
| `endReason` | `string?` | Why the call ended |
| `maxParticipants` | `int` | Peak participant count |
| `isGroupCall` | `bool` | Whether it was a group call |

**Errors:**
- `403` — Not a channel member

---

### Get Call by ID

```
GET /api/v1/chat/calls/{callId}?userId={userId}
```

**Response:** `200 OK` → `VideoCallDto`

**Errors:**
- `403` — Not authorized
- `404` — Call not found

---

### Get Active Call

```
GET /api/v1/chat/channels/{channelId}/calls/active?userId={userId}
```

Returns the currently active call in a channel, or `null` if no call is active.

**Response:** `200 OK` → `VideoCallDto` or `{ "data": null }`

**Errors:**
- `403` — Not a channel member

---

### Get ICE Servers

```
GET /api/v1/chat/ice-servers?userId={userId}
```

Returns ICE server configuration for WebRTC peer connections.

**Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "iceServers": [
      { "urls": ["stun:example.com:3478"] },
      { "urls": ["turn:turn.example.com:3478"], "username": "...", "credential": "..." }
    ],
    "iceTransportPolicy": "all"
  }
}
```

| Field | Type | Description |
|---|---|---|
| `iceServers` | `IceServerDto[]` | STUN and TURN server configurations |
| `iceTransportPolicy` | `string` | `all` or `relay` |

---

## Video Call gRPC RPCs

The Chat module also exposes video call operations via gRPC for inter-process communication:

| RPC | Request | Response | Description |
|---|---|---|---|
| `InitiateVideoCall` | `InitiateVideoCallRequest` | `VideoCallResponse` | Start a call in a channel |
| `JoinVideoCall` | `JoinVideoCallRequest` | `VideoCallResponse` | Join an active call |
| `LeaveVideoCall` | `LeaveVideoCallRequest` | `VideoCallOperationResponse` | Leave a call |
| `EndVideoCall` | `EndVideoCallRequest` | `VideoCallOperationResponse` | End a call for all |
| `RejectVideoCall` | `RejectVideoCallRequest` | `VideoCallOperationResponse` | Reject an incoming call |
| `GetCallHistory` | `GetCallHistoryRequest` | `GetCallHistoryResponse` | Paginated call history |
| `GetActiveCall` | `GetActiveCallRequest` | `VideoCallResponse` | Get active call in channel |

---

## gRPC Service

The Chat module exposes a gRPC service for inter-process communication with the core supervisor.

### Service Definition

```protobuf
service ChatService {
  rpc CreateChannel (CreateChannelRequest) returns (ChannelResponse);
  rpc GetChannel (GetChannelRequest) returns (ChannelResponse);
  rpc ListChannels (ListChannelsRequest) returns (ListChannelsResponse);
  rpc SendMessage (SendMessageRequest) returns (MessageResponse);
  rpc GetMessages (GetMessagesRequest) returns (GetMessagesResponse);
  rpc EditMessage (EditMessageRequest) returns (MessageResponse);
  rpc DeleteMessage (DeleteMessageRequest) returns (DeleteMessageResponse);
  rpc AddReaction (AddReactionRequest) returns (ReactionResponse);
  rpc RemoveReaction (RemoveReactionRequest) returns (ReactionResponse);
  rpc NotifyTyping (TypingRequest) returns (TypingResponse);
}
```

See the [Architecture documentation](ARCHITECTURE.md) for gRPC message type details.

---

## Health & Module Info

### Health Check

```
GET /health
```

**Response:** `200 OK` → Standard ASP.NET Core health check response

### Module Info

```
GET /api/v1/chat/info
```

**Response:** `200 OK` → Module manifest information (ID, name, version, capabilities)

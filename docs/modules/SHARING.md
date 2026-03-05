# Files Module — Sharing Guide

> **Last Updated:** 2026-03-03

---

## Overview

The Files module supports four types of sharing, each designed for different collaboration scenarios. Shares can be applied to both files and folders, with folder shares cascading to all children.

---

## Share Types

### User Share

Share a file or folder directly with a specific user by their ID.

```json
{
  "shareType": "User",
  "sharedWithUserId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "permission": "ReadWrite"
}
```

- The recipient sees the item in their "Shared with me" view
- A `FileSharedEvent` is published, enabling notification delivery

### Team Share

Share with all members of a team. Any current or future member of the team gains access.

```json
{
  "shareType": "Team",
  "sharedWithTeamId": "7ca85f64-5717-4562-b3fc-2c963f66afa6",
  "permission": "Read"
}
```

### Group Share

Share with a cross-team permission group. Groups span multiple teams within an organization.

```json
{
  "shareType": "Group",
  "sharedWithGroupId": "9da85f64-5717-4562-b3fc-2c963f66afa6",
  "permission": "ReadWrite"
}
```

### Public Link

Generate a shareable URL that anyone with the link can access. No DotNetCloud account is required.

```json
{
  "shareType": "PublicLink",
  "permission": "Read",
  "linkPassword": "secret123",
  "maxDownloads": 10,
  "expiresAt": "2026-06-01T00:00:00Z",
  "note": "Budget report for Q1 review"
}
```

---

## Permission Levels

| Level | View | Download | Upload | Rename/Move | Re-share | Delete |
|---|---|---|---|---|---|---|
| **Read** | ✓ | ✓ | ✗ | ✗ | ✗ | ✗ |
| **ReadWrite** | ✓ | ✓ | ✓ | ✓ | ✗ | ✗ |
| **Full** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

### Permission Cascade

When a folder is shared, the permission level applies to all files and subfolders within it. There is no per-child override; the share applies uniformly to the entire subtree.

---

## Public Link Features

### Link Token

Each public link receives a cryptographically random token (URL-safe, 32 characters). The link URL format is:

```
https://cloud.example.com/api/v1/files/public/{linkToken}
```

### Password Protection

Public links can optionally require a password. Passwords are hashed using ASP.NET Identity's `PasswordHasher` before storage — plain-text passwords are never stored.

To access a password-protected link:

```
GET /api/v1/files/public/{linkToken}?password=secret123
```

### Download Limits

Set `maxDownloads` to restrict how many times the file can be downloaded via the public link. The `downloadCount` is incremented on each successful download. When `downloadCount >= maxDownloads`, the link returns a `FILES_SHARE_DOWNLOAD_LIMIT` error.

### Expiration

Set `expiresAt` to automatically disable the link after a specific date/time. Expired links return a `FILES_SHARE_EXPIRED` error.

---

## Share Lifecycle

### Creating a Share

```
POST /api/v1/files/{nodeId}/shares?userId={guid}
```

1. Validate the caller owns the file or has `Full` permission
2. Create the `FileShare` record
3. For public links: generate a random `LinkToken` and hash the password (if provided)
4. Publish `FileSharedEvent` for notification delivery
5. Return the share DTO

### Viewing Shares

**Shares on a node:**

```
GET /api/v1/files/{nodeId}/shares?userId={guid}
```

**Shared with me:**

```
GET /api/v1/files/shared-with-me?userId={guid}
```

### Updating a Share

```
PUT /api/v1/files/{nodeId}/shares/{shareId}?userId={guid}
```

Update permission level, expiration, max downloads, password, or note.

### Revoking a Share

```
DELETE /api/v1/files/{nodeId}/shares/{shareId}?userId={guid}
```

Immediately removes access. The `LinkToken` becomes invalid.

---

## Share Behavior on File Operations

| Operation | Effect on Shares |
|---|---|
| **Move** | Shares remain active (they reference the node ID, not the path) |
| **Rename** | Shares remain active |
| **Trash (soft-delete)** | Active shares are removed |
| **Restore from trash** | Shares are not automatically restored |
| **Permanent delete** | Shares are cascade-deleted |
| **Copy** | Shares are **not** copied to the new node |

---

## Data Model

### FileShare Entity

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Primary key |
| `FileNodeId` | `Guid` | FK to the shared node |
| `ShareType` | `ShareType` | `User`, `Team`, `Group`, or `PublicLink` |
| `SharedWithUserId` | `Guid?` | Target user (User shares) |
| `SharedWithTeamId` | `Guid?` | Target team (Team shares) |
| `SharedWithGroupId` | `Guid?` | Target group (Group shares) |
| `Permission` | `SharePermission` | `Read`, `ReadWrite`, or `Full` |
| `LinkToken` | `string?` | Random URL token (PublicLink only) |
| `LinkPasswordHash` | `string?` | Hashed password (PublicLink only) |
| `MaxDownloads` | `int?` | Download limit (PublicLink only) |
| `DownloadCount` | `int` | Current download count |
| `ExpiresAt` | `DateTime?` | Expiration timestamp |
| `CreatedByUserId` | `Guid` | Share creator |
| `CreatedAt` | `DateTime` | Creation timestamp |
| `Note` | `string?` | Optional note |

---

## API Reference

See the complete endpoint documentation in [API.md](API.md#share-endpoints).

# DotNetCloud Files Module

> **Module ID:** `dotnetcloud.files`  
> **Version:** 1.0.0  
> **Status:** Implemented (Phase 1)  
> **License:** AGPL-3.0

---

## Overview

The Files module is the primary storage and file management component of DotNetCloud. It provides a complete file management platform with chunked uploads, content-hash deduplication, versioning, sharing, trash/recovery, quota management, and browser-based document editing via Collabora Online.

## Key Features

| Feature | Description |
|---|---|
| **Chunked Upload** | Files are split into 4 MB chunks, hashed with SHA-256, and deduplicated server-side |
| **Content-Hash Deduplication** | Identical chunks are stored once, regardless of how many files reference them |
| **File Versioning** | Every content update creates a new version; restore any previous version at any time |
| **Sharing** | Share files/folders with users, teams, groups, or via password-protected public links |
| **Trash & Recovery** | Soft-delete with configurable auto-cleanup; restore individual items or empty trash |
| **Storage Quotas** | Per-user quota enforcement with configurable warning/critical thresholds |
| **Collabora/WOPI** | Browser-based document editing (Word, Excel, PowerPoint) via Collabora CODE |
| **Desktop Sync** | Bidirectional sync client with conflict detection and selective sync |
| **Tags & Comments** | Organize files with colored tags; threaded comment discussions per file |
| **Bulk Operations** | Move, copy, or delete multiple files/folders in a single request |
| **Thumbnail Generation** | Automatic thumbnail creation for image files using ImageSharp |
| **Right-Click Context Menu** | Context menu on file/folder items for rename, move, copy, share, download, and delete |
| **Drag-and-Drop Move** | Drag files or folders onto a folder to move them; visual drop-target highlighting |
| **Upload Queue Control** | Per-file pause, resume, and cancel with chunk-level `AbortController` support |
| **Paste Image Upload** | Paste images from clipboard (Ctrl+V) — auto-generates timestamped filenames |
| **Upload Size Validation** | Client-side file size check before upload; configurable maximum via server settings |
| **OpenAPI Documentation** | Interactive Scalar API explorer at `/scalar/v1` (development mode) |

## Architecture

The Files module follows the DotNetCloud module architecture pattern with three projects:

```
src/Modules/Files/
├── DotNetCloud.Modules.Files/          # Core domain models, DTOs, interfaces, UI components
├── DotNetCloud.Modules.Files.Data/     # EF Core context, entity configs, service implementations
└── DotNetCloud.Modules.Files.Host/     # ASP.NET Core host: REST controllers, gRPC services
```

### Module Manifest

The `FilesModuleManifest` declares:

- **Required Capabilities:** `INotificationService`, `IStorageProvider`, `IUserDirectory`, `ICurrentUserContext`
- **Published Events:** `FileUploadedEvent`, `FileDeletedEvent`, `FileMovedEvent`, `FileSharedEvent`, `FileRestoredEvent`
- **Subscribed Events:** (none)

### Data Flow

```
Client → REST API / gRPC → Service Layer → EF Core → Database
                                         → IFileStorageEngine → Disk Storage
```

## Project Structure

### DotNetCloud.Modules.Files (Core)

| Directory | Contents |
|---|---|
| `Models/` | Entity models (`FileNode`, `FileVersion`, `FileChunk`, `FileShare`, etc.) |
| `DTOs/` | Data transfer objects for API requests/responses |
| `Events/` | Domain events implementing `IEvent` |
| `Services/` | Service interfaces (`IFileService`, `IChunkedUploadService`, etc.) |
| `Options/` | Configuration options (`CollaboraOptions`, `QuotaOptions`, etc.) |
| `UI/` | Blazor components (`FileBrowser`, `FilePreview`, `ShareDialog`, etc.) |

### DotNetCloud.Modules.Files.Data (Data Access)

| Directory | Contents |
|---|---|
| `Configuration/` | EF Core `IEntityTypeConfiguration` for all entities |
| `Services/` | Service implementations (`FileService`, `ChunkedUploadService`, etc.) |
| `Services/Background/` | Background services (`TrashCleanupService`, `UploadSessionCleanupService`, etc.) |
| `Migrations/` | PostgreSQL and SQL Server migrations |

### DotNetCloud.Modules.Files.Host (Web Host)

| Directory | Contents |
|---|---|
| `Controllers/` | REST API controllers (`FilesController`, `ShareController`, `WopiController`, etc.) |
| `Services/` | gRPC services (`FilesGrpcService`, `FilesLifecycleService`) |
| `Protos/` | Protobuf service definitions |

## Database Support

| Provider | Status |
|---|---|
| PostgreSQL | ✅ Supported (schema: `files.*`) |
| SQL Server | ✅ Supported (schema: `files.*`) |
| MariaDB | ⏳ Pending (awaiting Pomelo .NET 10 support) |

## Configuration

Configuration is managed via `appsettings.json` sections:

| Section | Options Class | Purpose |
|---|---|---|
| `Files:Quota` | `QuotaOptions` | Default quota, warning/critical thresholds |
| `Files:TrashRetention` | `TrashRetentionOptions` | Trash auto-cleanup interval and retention period |
| `Files:VersionRetention` | `VersionRetentionOptions` | Max versions per file, time-based retention |
| `Files:Collabora` | `CollaboraOptions` | Collabora CODE server URL, token settings, session limits |
| `FileUpload` | `FileUploadOptions` | Maximum upload file size (`MaxFileSizeBytes`, default 15 GB) |

## Related Documentation

- [REST API Reference](API.md)
- [Architecture & Data Model](ARCHITECTURE.md)
- [Sharing Guide](SHARING.md)
- [Versioning Guide](VERSIONING.md)
- [WOPI / Collabora Integration](WOPI.md)
- [Desktop Sync Protocol](SYNC.md)
- [Admin Configuration](../admin/CONFIGURATION.md)
- [User Getting Started](../user/GETTING_STARTED.md)

## Test Coverage

| Test Project | Tests | Description |
|---|---|---|
| `DotNetCloud.Modules.Files.Tests` | 276+ | Unit tests for all services, models, events, and utilities |
| `DotNetCloud.Client.Core.Tests` | 53 | Sync engine, chunked transfer, API client, OAuth2, selective sync |

## Getting Started (Developer)

1. Ensure PostgreSQL or SQL Server is available (see [Database Setup](../../development/DATABASE_SETUP.md))
2. Build the solution: `dotnet build`
3. Run the Files host: `dotnet run --project src/Modules/Files/DotNetCloud.Modules.Files.Host`
4. Access the API at `https://localhost:5001/api/v1/files`
5. Run tests: `dotnet test tests/DotNetCloud.Modules.Files.Tests`

# DotNetCloud.Modules.Files

Core domain library for the DotNetCloud Files module. Contains models, DTOs, service interfaces, events, configuration options, and Blazor UI components.

## Project Structure

```
DotNetCloud.Modules.Files/
├── Models/                 # EF Core entity models
│   ├── FileNode.cs         # File/folder tree node
│   ├── FileVersion.cs      # Version history entry
│   ├── FileChunk.cs        # Content-addressed chunk
│   ├── FileVersionChunk.cs # Version ↔ Chunk junction
│   ├── FileShare.cs        # Sharing record
│   ├── FileTag.cs          # Colored tag
│   ├── FileComment.cs      # Threaded comment
│   ├── FileQuota.cs        # Per-user quota
│   ├── ChunkedUploadSession.cs # Upload progress tracking
│   └── Enums               # FileNodeType, ShareType, SharePermission, UploadSessionStatus
│
├── DTOs/                   # Data transfer objects
│   ├── FileDtos.cs         # FileNodeDto, CreateFolderDto, UploadSessionDto, etc.
│   ├── SyncDtos.cs         # SyncChangeDto, SyncTreeNodeDto, SyncReconcileRequestDto
│   ├── WopiDtos.cs         # WopiCheckFileInfoResponse, WopiTokenDto
│   ├── StorageMetricsDto.cs
│   └── PagedResult.cs      # Generic paged result wrapper
│
├── Services/               # Service interfaces
│   ├── IFileService.cs             # File/folder CRUD, move, copy, favorites, search
│   ├── IChunkedUploadService.cs    # Chunked upload session management
│   ├── IDownloadService.cs         # File/version/chunk download
│   ├── IVersionService.cs          # Version history, restore, label
│   ├── IShareService.cs            # User/team/group/public link sharing
│   ├── ITrashService.cs            # Soft-delete, restore, permanent delete
│   ├── IQuotaService.cs            # Storage quota enforcement
│   ├── ITagService.cs              # Tag management
│   ├── ICommentService.cs          # Threaded comments
│   ├── ISyncService.cs             # Server-side sync endpoints
│   ├── IPermissionService.cs       # Permission validation
│   ├── IFileStorageEngine.cs       # Storage backend abstraction
│   ├── IWopiService.cs             # WOPI CheckFileInfo/GetFile/PutFile
│   ├── IWopiTokenService.cs        # WOPI token generation/validation
│   ├── IWopiSessionTracker.cs      # Concurrent session limits
│   ├── IWopiProofKeyValidator.cs   # Collabora proof key validation
│   ├── ICollaboraDiscoveryService.cs # Collabora WOPI discovery
│   ├── ICollaboraProcessManager.cs # Built-in CODE process management
│   ├── IStorageMetricsService.cs   # Deduplication and storage metrics
│   ├── IThumbnailService.cs        # Image thumbnail generation
│   ├── ContentHasher.cs            # SHA-256 hashing utility
│   ├── LocalFileStorageEngine.cs   # Disk-based storage engine
│   └── ThumbnailService.cs         # ImageSharp thumbnail generator
│
├── Events/                 # Domain events (implement IEvent)
│   ├── FileUploadedEvent.cs
│   ├── FileDeletedEvent.cs
│   ├── FileMovedEvent.cs
│   ├── FileSharedEvent.cs
│   ├── FileRestoredEvent.cs
│   ├── FileVersionRestoredEvent.cs
│   ├── QuotaWarningEvent.cs
│   ├── QuotaCriticalEvent.cs
│   ├── QuotaExceededEvent.cs
│   └── FileUploadedEventHandler.cs
│
├── Options/                # Configuration options
│   ├── CollaboraOptions.cs         # Collabora CODE/WOPI settings
│   ├── QuotaOptions.cs             # Default quota, thresholds
│   ├── TrashRetentionOptions.cs    # Trash auto-cleanup
│   └── VersionRetentionOptions.cs  # Version retention policies
│
├── UI/                     # Blazor components
│   ├── FileBrowser.razor.cs        # Main file browser (grid/list view)
│   ├── FilePreview.razor.cs        # Image/video/audio/PDF/text preview
│   ├── FileUploadComponent.razor.cs # Drag-and-drop upload
│   ├── UploadProgressPanel.razor.cs # Per-file progress tracking
│   ├── ShareDialog.razor.cs        # Share creation and management
│   ├── TrashBin.razor.cs           # Trash bin browser
│   ├── VersionHistoryPanel.razor.cs # Version history side panel
│   ├── FileSidebar.razor.cs        # Navigation sidebar
│   ├── FilesAdminSettings.razor.cs # Admin settings page
│   ├── DocumentEditor.razor.cs     # Collabora iframe editor
│   ├── SharedWithMeView.razor.cs   # Files shared with current user
│   ├── SharedByMeView.razor.cs     # Files shared by current user
│   ├── QuotaProgressBar.razor.cs   # Quota usage bar
│   ├── TagInput.razor.cs           # Tag autocomplete input
│   ├── TagBadge.razor.cs           # Colored tag badge
│   └── ViewModels.cs               # UI view model records
│
└── FilesModuleManifest.cs  # Module manifest (IModuleManifest)
    FilesModule.cs          # Module lifecycle (IModuleLifecycle)
    FilesErrorCodes.cs      # Error code constants
```

## Dependencies

- `DotNetCloud.Core` — Core abstractions (IEvent, IModule, CallerContext, etc.)
- `SixLabors.ImageSharp` — Thumbnail generation

## Related Projects

| Project | Purpose |
|---|---|
| `DotNetCloud.Modules.Files.Data` | EF Core context, entity configurations, service implementations |
| `DotNetCloud.Modules.Files.Host` | REST controllers, gRPC services, ASP.NET Core host |
| `DotNetCloud.Modules.Files.Tests` | Unit tests (276+ tests) |

## Documentation

- [Module Overview](../../../docs/modules/files/README.md)
- [REST API Reference](../../../docs/modules/files/API.md)
- [Architecture & Data Model](../../../docs/modules/files/ARCHITECTURE.md)
- [Sharing Guide](../../../docs/modules/files/SHARING.md)
- [Versioning Guide](../../../docs/modules/files/VERSIONING.md)
- [WOPI / Collabora](../../../docs/modules/files/WOPI.md)
- [Desktop Sync](../../../docs/modules/files/SYNC.md)

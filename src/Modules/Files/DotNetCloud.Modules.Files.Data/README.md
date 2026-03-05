# DotNetCloud.Modules.Files.Data

EF Core data access layer for the DotNetCloud Files module. Contains the database context, entity configurations, service implementations, background services, and migrations.

## Project Structure

```
DotNetCloud.Modules.Files.Data/
├── Configuration/              # EF Core entity type configurations
│   ├── FileNodeConfiguration.cs
│   ├── FileVersionConfiguration.cs
│   ├── FileChunkConfiguration.cs
│   ├── FileVersionChunkConfiguration.cs
│   ├── FileShareConfiguration.cs
│   ├── FileTagConfiguration.cs
│   ├── FileCommentConfiguration.cs
│   ├── FileQuotaConfiguration.cs
│   └── ChunkedUploadSessionConfiguration.cs
│
├── Services/                   # Service implementations
│   ├── FileService.cs                  # File/folder CRUD, move, copy, favorites, search
│   ├── ChunkedUploadService.cs         # Chunked upload with deduplication
│   ├── DownloadService.cs              # File/version/chunk download
│   ├── VersionService.cs              # Version history, restore, label
│   ├── ShareService.cs                # User/team/group/public link sharing
│   ├── TrashService.cs                # Soft-delete, restore, permanent delete
│   ├── QuotaService.cs                # Storage quota enforcement
│   ├── TagService.cs                  # Tag management
│   ├── CommentService.cs             # Threaded comments
│   ├── SyncService.cs                 # Server-side sync endpoints
│   ├── PermissionService.cs           # Permission validation
│   ├── StorageMetricsService.cs       # Deduplication and storage metrics
│   ├── WopiService.cs                 # WOPI CheckFileInfo/GetFile/PutFile
│   ├── WopiTokenService.cs            # WOPI token generation/validation
│   ├── WopiSessionTracker.cs          # Concurrent session limits
│   ├── WopiProofKeyValidator.cs       # Collabora proof key validation
│   ├── CollaboraDiscoveryService.cs   # WOPI discovery endpoint
│   ├── CollaboraProcessManager.cs     # Built-in CODE process management
│   └── CollaboraHealthCheck.cs        # Collabora health monitoring
│
├── Services/Background/       # BackgroundService implementations
│   ├── UploadSessionCleanupService.cs   # Expire stale uploads, GC chunks
│   ├── TrashCleanupService.cs           # Auto-delete expired trash
│   ├── QuotaRecalculationService.cs     # Recalculate user quotas
│   └── VersionCleanupService.cs         # Prune old versions
│
├── Migrations/                 # EF Core migrations
│   ├── 20260304172504_InitialFilesSchema.cs           # PostgreSQL
│   └── SqlServer/
│       └── 20260304172718_InitialFilesSchema_SqlServer.cs  # SQL Server
│
├── FilesDbContext.cs           # EF Core DbContext
├── FilesDbContextDesignTimeFactory.cs  # Design-time factory for migrations
├── FilesDbInitializer.cs      # Database seeding
└── FilesServiceRegistration.cs # DI service registration
```

## Dependencies

- `DotNetCloud.Modules.Files` — Core domain models and interfaces
- `DotNetCloud.Core` — Core abstractions (IEvent, CallerContext, etc.)
- `DotNetCloud.Core.Data` — Shared database infrastructure (naming strategies)
- `Microsoft.EntityFrameworkCore` — ORM
- `Npgsql.EntityFrameworkCore.PostgreSQL` — PostgreSQL provider
- `Microsoft.EntityFrameworkCore.SqlServer` — SQL Server provider

## Database Schema

All tables live under the `files` schema (PostgreSQL/SQL Server) or use the `files_` prefix (MariaDB).

## Related Projects

| Project | Purpose |
|---|---|
| `DotNetCloud.Modules.Files` | Core domain models, DTOs, service interfaces |
| `DotNetCloud.Modules.Files.Host` | REST controllers, gRPC services |
| `DotNetCloud.Modules.Files.Tests` | Unit tests |

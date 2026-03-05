# DotNetCloud.Modules.Files.Host

ASP.NET Core web host for the DotNetCloud Files module. Provides REST API controllers, gRPC services, health checks, and the module's HTTP pipeline.

## Project Structure

```
DotNetCloud.Modules.Files.Host/
├── Controllers/                # REST API controllers
│   ├── FilesControllerBase.cs        # Base controller with envelope helpers
│   ├── FilesController.cs            # File/folder CRUD, upload, download
│   ├── VersionController.cs          # Version history, restore, label
│   ├── ShareController.cs            # Create/update/delete shares per node
│   ├── MySharesController.cs         # Shared-with-me, shared-by-me views
│   ├── PublicShareController.cs      # Public link access (no auth required)
│   ├── TrashController.cs            # Trash list, restore, purge, empty
│   ├── QuotaController.cs            # Quota get/set/recalculate
│   ├── TagController.cs              # Tag add/remove/list
│   ├── CommentController.cs          # Comment add/edit/delete/list
│   ├── BulkController.cs             # Bulk move/copy/delete
│   ├── SyncController.cs             # Sync changes/tree/reconcile
│   ├── WopiController.cs             # WOPI CheckFileInfo/GetFile/PutFile/tokens
│   └── StorageMetricsController.cs   # Deduplication and storage stats
│
├── Services/                   # Host-level services
│   ├── FilesGrpcService.cs           # gRPC service implementation
│   ├── FilesLifecycleService.cs      # Module lifecycle gRPC
│   ├── FilesHealthCheck.cs           # Health check implementation
│   └── InProcessEventBus.cs         # In-process event bus
│
├── Protos/                     # gRPC service definitions
│   └── files_service.proto
│
└── Program.cs                  # Host entry point and DI configuration
```

## API Base URL

All Files REST endpoints are served under `/api/v1/files/`.

## Running

```powershell
dotnet run --project src\Modules\Files\DotNetCloud.Modules.Files.Host
```

The host starts on `https://localhost:5001` by default.

## Dependencies

- `DotNetCloud.Modules.Files` — Core domain models and interfaces
- `DotNetCloud.Modules.Files.Data` — EF Core context and service implementations
- `DotNetCloud.Core.Grpc` — gRPC interceptors and infrastructure
- `DotNetCloud.Core.ServiceDefaults` — Serilog, OpenTelemetry, health checks
- `Grpc.AspNetCore` — gRPC server

## Related Projects

| Project | Purpose |
|---|---|
| `DotNetCloud.Modules.Files` | Core domain models, DTOs, service interfaces |
| `DotNetCloud.Modules.Files.Data` | EF Core context, service implementations |
| `DotNetCloud.Modules.Files.Tests` | Unit tests |

## Documentation

- [REST API Reference](../../../docs/modules/files/API.md)
- [Module Overview](../../../docs/modules/files/README.md)
- [Architecture](../../../docs/modules/files/ARCHITECTURE.md)

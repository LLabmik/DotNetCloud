# DotNetCloud.Modules.Chat.Host

ASP.NET Core host process for the DotNetCloud Chat module. Exposes the chat functionality through a REST API, gRPC service, and SignalR real-time events.

## Assembly Name

```
dotnetcloud.chat
```

This is the process name used by the core supervisor when launching the chat module.

## Project Structure

```
DotNetCloud.Modules.Chat.Host/
├── Program.cs                  # Host configuration and DI setup
├── Controllers/
│   └── ChatController.cs      # Unified REST API (~30 endpoints)
├── Services/
│   ├── ChatGrpcService.cs     # gRPC server implementation
│   ├── ChatLifecycleService.cs # Module lifecycle management
│   ├── ChatHealthCheck.cs     # Health check endpoint
│   └── InProcessEventBus.cs   # In-process event bus for dev
├── Protos/
│   └── chat_service.proto     # gRPC service definitions
└── Properties/
    └── launchSettings.json
```

## API Endpoints

The `ChatController` exposes all chat endpoints under `/api/v1/chat/`:

| Group | Endpoints | Description |
|-------|-----------|-------------|
| Channels | `POST/GET/PUT/DELETE /channels` | Channel CRUD + listing |
| Members | `POST/DELETE/GET /channels/{id}/members` | Join, leave, list members |
| Messages | `POST/GET/PUT/DELETE /channels/{id}/messages` | Message CRUD + history |
| Reactions | `POST/DELETE /messages/{id}/reactions` | Add/remove emoji reactions |
| Pins | `POST/DELETE/GET /channels/{id}/pins` | Pin/unpin messages |
| Typing | `POST/DELETE /channels/{id}/typing` | Typing indicators |
| Announcements | `POST/GET/PUT/DELETE /channels/{id}/announcements` | Channel announcements |
| Attachments | `POST /messages/{id}/attachments` | File attachments |
| Push | `POST/DELETE /devices/register` | Push notification registration |
| Read Status | `POST /channels/{id}/read` | Mark channel as read |
| Health | `GET /health`, `GET /info` | Health check and module info |

Full API reference: [docs/modules/chat/API.md](../../../docs/modules/chat/API.md)

## gRPC Service

`ChatGrpcService` implements the server-side gRPC contract defined in `chat_service.proto`. The core supervisor communicates with the chat module exclusively through this gRPC interface.

## Running

### Standalone (Development)

```bash
dotnet run --project src/Modules/Chat/DotNetCloud.Modules.Chat.Host
```

Uses EF Core InMemory database by default for development.

### As a Module (Production)

The core supervisor launches the chat host as a child process:

```
dotnetcloud (core process)
└── dotnetcloud.chat (this host) ← communicates via gRPC over Unix socket
```

## Dependencies

- `Grpc.AspNetCore` 2.71.0 — gRPC server
- `Microsoft.EntityFrameworkCore.InMemory` 10.0.3 — development database
- `DotNetCloud.Modules.Chat` — domain models and interfaces
- `DotNetCloud.Modules.Chat.Data` — EF Core data layer
- `DotNetCloud.Core.Grpc` — shared gRPC infrastructure

## Testing

Integration tests are in `tests/DotNetCloud.Integration.Tests/`:

```bash
dotnet test tests/DotNetCloud.Integration.Tests/
```

The test infrastructure uses `WebApplicationFactory<Program>` with extern alias `ChatHost` for isolated multi-host testing.

## Documentation

- [REST API Reference](../../../docs/modules/chat/API.md)
- [Architecture](../../../docs/modules/chat/ARCHITECTURE.md)
- [Real-time Events](../../../docs/modules/chat/REALTIME.md)
- [Push Notifications](../../../docs/modules/chat/PUSH.md)

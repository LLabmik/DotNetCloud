# DotNetCloud.Modules.Chat.Data

EF Core data layer for the DotNetCloud Chat module. Contains the `ChatDbContext`, entity configurations, database migrations, and concrete service implementations.

## Project Structure

```
DotNetCloud.Modules.Chat.Data/
├── ChatDbContext.cs                    # EF Core DbContext
├── ChatDbContextDesignTimeFactory.cs   # Design-time factory for migrations
├── ChatDbInitializer.cs               # Seed data / initial setup
├── ChatServiceRegistration.cs         # DI extension methods
├── Configuration/                     # Entity type configurations
│   ├── ChannelConfiguration.cs
│   ├── ChannelMemberConfiguration.cs
│   ├── MessageConfiguration.cs
│   ├── MessageAttachmentConfiguration.cs
│   ├── MessageReactionConfiguration.cs
│   ├── MessageMentionConfiguration.cs
│   ├── PinnedMessageConfiguration.cs
│   ├── AnnouncementConfiguration.cs
│   └── AnnouncementAcknowledgementConfiguration.cs
├── Services/                          # Interface implementations
│   ├── ChannelService.cs
│   ├── ChannelMemberService.cs
│   ├── MessageService.cs
│   ├── ReactionService.cs
│   ├── PinService.cs
│   ├── TypingIndicatorService.cs
│   ├── AnnouncementService.cs
│   └── MentionNotificationService.cs
└── Migrations/
    └── 20260304135720_InitialCreate.cs
```

## Database Support

| Provider | Status | Package |
|----------|--------|---------|
| PostgreSQL | Supported | `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0 |
| SQL Server | Supported | `Microsoft.EntityFrameworkCore.SqlServer` 10.0.3 |
| MariaDB | Pending | Awaiting Pomelo .NET 10 release |

## DbContext

`ChatDbContext` exposes the following `DbSet` properties:

| DbSet | Entity |
|-------|--------|
| `Channels` | `Channel` |
| `ChannelMembers` | `ChannelMember` |
| `Messages` | `Message` |
| `MessageAttachments` | `MessageAttachment` |
| `MessageReactions` | `MessageReaction` |
| `MessageMentions` | `MessageMention` |
| `PinnedMessages` | `PinnedMessage` |
| `Announcements` | `Announcement` |
| `AnnouncementAcknowledgements` | `AnnouncementAcknowledgement` |

All entities use soft-delete query filters and automatic timestamp interceptors inherited from the core framework.

## Entity Configurations

Each entity has a dedicated `IEntityTypeConfiguration<T>` that defines:
- Table names (schema-aware for PostgreSQL/SQL Server)
- Indexes for common query patterns
- Relationships and cascade behavior
- Required/optional field constraints
- Soft-delete global query filters

## Service Implementations

| Service | Interface | Key Operations |
|---------|-----------|---------------|
| `ChannelService` | `IChannelService` | CRUD, search by name/type |
| `ChannelMemberService` | `IChannelMemberService` | Join/leave, role management, last-owner protection |
| `MessageService` | `IMessageService` | Send, edit, delete, paginated history |
| `ReactionService` | `IReactionService` | Add/remove/toggle, normalized emoji validation |
| `PinService` | `IPinService` | Pin/unpin, `PinnedAt` timestamp preservation |
| `TypingIndicatorService` | `ITypingIndicatorService` | Start/stop with in-memory TTL cache |
| `AnnouncementService` | `IAnnouncementService` | Channel-scoped announcements + acknowledgements |
| `MentionNotificationService` | `IMentionNotificationService` | Unread count with `@all`/`@channel` support |

## Migrations

### Add a New Migration

```bash
# PostgreSQL (default)
dotnet ef migrations add <MigrationName> \
  --project src/Modules/Chat/DotNetCloud.Modules.Chat.Data \
  --context ChatDbContext

# SQL Server
dotnet ef migrations add <MigrationName>_SqlServer \
  --project src/Modules/Chat/DotNetCloud.Modules.Chat.Data \
  --context ChatDbContext \
  --output-dir Migrations/SqlServer
```

### Apply Migrations

```bash
dotnet ef database update --context ChatDbContext
```

## Dependency Injection

Register all chat data services with:

```csharp
services.AddChatServices(connectionString, databaseProvider);
```

This is called from `ChatServiceRegistration.AddChatServices()`.

## Dependencies

- `Microsoft.EntityFrameworkCore` 10.0.3
- `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.0
- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.3
- `DotNetCloud.Modules.Chat` — domain models and interfaces
- `DotNetCloud.Core.Data` — shared EF infrastructure

## Testing

Unit tests are in `tests/DotNetCloud.Modules.Chat.Tests/` using EF Core InMemory provider:

```bash
dotnet test tests/DotNetCloud.Modules.Chat.Tests/
```

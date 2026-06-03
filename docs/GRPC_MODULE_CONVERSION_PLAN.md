# gRPC Module Conversion Plan

> **Status:** Plan — awaiting implementation  
> **Date:** 2026-06-03  
> **Scope:** Convert 12 remaining modules to process-isolated gRPC + mandate gRPC in documentation

---

**TL;DR:** Contacts and Calendar are already process-isolated via gRPC. All 12 remaining modules still run in-process (Host projects referenced directly by `Core.Server.csproj`, business services registered via `AddXxxServices()` in `Core.Server/Program.cs`). Convert each module to run as a separate OS process communicating exclusively via gRPC over Unix sockets (Linux) or Named Pipes (Windows), following the Contacts/Calendar pattern exactly. Additionally, update all project documentation to make gRPC the **mandatory and enforced** inter-module communication protocol — no exceptions for current or future modules.

---

## 1. Current State: Full Module Inventory

### 1.1 Already Process-Isolated (Reference Implementations)

| Module   | Proto                             | GrpcService           | LifecycleService           | Host in Core.Server refs | gRPC Client in Core.Server |
| -------- | --------------------------------- | --------------------- | -------------------------- | ------------------------ | -------------------------- |
| Contacts | `contacts_service.proto` (Server) | `ContactsGrpcService` | `ContactsLifecycleService` | ❌ Removed               | `ContactsGrpcApiClient`    |
| Calendar | `calendar_service.proto` (Server) | `CalendarGrpcService` | `CalendarLifecycleService` | ❌ Removed               | `CalendarGrpcApiClient`    |

### 1.2 Full gRPC Infra But Still In-Process

These have proto + GrpcService + LifecycleService, but Core.Server still references their Host project and calls `AddXxxServices()` directly.

| Module    | Proto                              | GrpcService                                   | LifecycleService            | gRPC Client               | manifest.json |
| --------- | ---------------------------------- | --------------------------------------------- | --------------------------- | ------------------------- | ------------- |
| Chat      | `chat_service.proto` (Server)      | `ChatGrpcService : ChatServiceBase`           | `ChatLifecycleService`      | ❌                        | ❌            |
| Files     | `files_service.proto` (Both)       | `FilesGrpcService : FilesServiceBase`         | `FilesLifecycleService`     | ❌                        | ❌            |
| Notes     | `notes_service.proto` (Server)     | `NotesGrpcService : NotesGrpcServiceBase`     | `NotesLifecycleService`     | ❌                        | ✓             |
| Tracks    | `tracks_service.proto` (Server)    | `TracksGrpcService : TracksGrpcServiceBase`   | `TracksLifecycleService`    | ❌                        | ❌            |
| Bookmarks | `bookmarks_service.proto` (Server) | `BookmarksGrpcService : BookmarksServiceBase` | `BookmarksLifecycleService` | ❌                        | ❌            |
| Email     | `email_service.proto` (Server)     | `EmailGrpcService : EmailServiceBase`         | `EmailLifecycleService`     | ✓ (full, incl. streaming) | ❌            |

### 1.3 Partial gRPC Infra — Missing LifecycleService

These have proto + GrpcService but NO LifecycleService (process supervisor cannot control them remotely).

| Module | Proto                           | GrpcService                                     | LifecycleService | gRPC Client | manifest.json |
| ------ | ------------------------------- | ----------------------------------------------- | ---------------- | ----------- | ------------- |
| Music  | `music_service.proto` (Server)  | `MusicGrpcServiceImpl : MusicGrpcServiceBase`   | ❌               | ❌          | ❌            |
| Photos | `photos_service.proto` (Server) | `PhotosGrpcServiceImpl : PhotosGrpcServiceBase` | ❌               | ❌          | ❌            |
| Video  | `video_service.proto` (Server)  | `VideoGrpcServiceImpl : VideoGrpcServiceBase`   | ❌               | ❌          | ❌            |
| Search | `search_service.proto` (Server) | `SearchGrpcService : SearchServiceBase`         | ❌               | ❌          | ❌            |

### 1.4 No gRPC At All

| Module | Proto | GrpcService | LifecycleService | gRPC Client | manifest.json |
| ------ | ----- | ----------- | ---------------- | ----------- | ------------- |
| AI     | ❌    | ❌          | ❌               | ❌          | ✓             |
| About  | ❌    | ❌          | ✓ (only)         | ❌          | ❌            |

---

## 2. Reference Architecture: How Contacts Was Converted

Every module conversion follows this exact pattern. Understanding this deeply is critical.

### 2.1 Module Host `.csproj` (Contacts)

File: `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/DotNetCloud.Modules.Contacts.Host.csproj`

Key properties and items:

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>dotnetcloud.contacts</AssemblyName>         <!-- MUST match module ID -->
    <RootNamespace>DotNetCloud.Modules.Contacts.Host</RootNamespace>
    <PublishWithPackageReferences>false</PublishWithPackageReferences>
    <PublishReadyToCompile>false</PublishReadyToCompile>
</PropertyGroup>

<ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" PrivateAssets="all" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="OpenIddict.Validation.AspNetCore" />
</ItemGroup>

<ItemGroup>
    <Protobuf Include="Protos\contacts_service.proto" GrpcServices="Server" />
</ItemGroup>

<ItemGroup>
    <ProjectReference Include="..\DotNetCloud.Modules.Contacts\DotNetCloud.Modules.Contacts.csproj" />
    <ProjectReference Include="..\DotNetCloud.Modules.Contacts.Data\DotNetCloud.Modules.Contacts.Data.csproj" />
    <ProjectReference Include="..\..\Calendar\DotNetCloud.Modules.Calendar.Data\DotNetCloud.Modules.Calendar.Data.csproj" />
    <ProjectReference Include="..\..\Notes\DotNetCloud.Modules.Notes.Data\DotNetCloud.Modules.Notes.Data.csproj" />
    <ProjectReference Include="..\..\..\Core\DotNetCloud.Core.Grpc\DotNetCloud.Core.Grpc.csproj" />
</ItemGroup>
```

Note: Contacts.Host references Calendar.Data and Notes.Data because the Contacts gRPC service provides `GetContactRelated` which queries across modules. The Calendar module host does NOT reference Contacts.Data — it uses a gRPC client (`ContactsGrpcClient` in Calendar.Host) to talk to Contacts — this is the CORRECT cross-module pattern.

### 2.2 Module Host `Program.cs` (Contacts)

File: `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/Program.cs`

Critical patterns:

1. **Config loading from shared config directory** — reads `DOTNETCLOUD_CONFIG_DIR` env var, loads `config.json` for database connection string:

```csharp
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir))
{
    var configJsonPath = Path.Combine(configDir, "config.json");
    if (File.Exists(configJsonPath))
        builder.Configuration.AddJsonFile(configJsonPath, optional: true, reloadOnChange: false);
}
```

2. **gRPC endpoint binding** — reads `DOTNETCLOUD_GRPC_ENDPOINT` (set by ProcessSupervisor), configures Kestrel for HTTP/2 on the assigned port:

```csharp
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if (!string.IsNullOrEmpty(grpcEndpoint))
{
    var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Listen(System.Net.IPAddress.Loopback, uri.Port, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });
    });
}
```

3. **Database configuration from shared config** — reads `connectionString` and `databaseProvider` from config, falls back to in-memory:

```csharp
var connStr = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connStr) && !string.IsNullOrEmpty(dbProvider))
{
    void ConfigureDb(DbContextOptionsBuilder o)
    {
        if (string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
            o.UseNpgsql(connStr);
        else
            o.UseSqlServer(connStr);
    }
    builder.Services.AddDbContext<ContactsDbContext>(ConfigureDb);
}
else
{
    builder.Services.AddDbContext<ContactsDbContext>(o => o.UseInMemoryDatabase("ContactsModule"));
}
```

4. **Service registration** — registers module singleton, business services, event bus, gRPC, controllers, health checks:

```csharp
builder.Services.AddSingleton<ContactsModule>();
builder.Services.AddSingleton<IFileValidationService, FileValidationService>();
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
builder.Services.AddContactsServices(builder.Configuration);
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddCheck<ContactsHealthCheck>("contacts_module");
```

5. **gRPC service mapping** — maps BOTH the domain gRPC service AND the lifecycle service:

```csharp
app.MapGrpcService<ContactsGrpcService>();
app.MapGrpcService<ContactsLifecycleService>();
app.MapControllers();
app.MapHealthChecks("/health");
```

### 2.3 LifecycleService Implementation Pattern

File: `src/Modules/About/DotNetCloud.Modules.About.Host/Services/AboutLifecycleService.cs`

Every module's LifecycleService follows this exact pattern:

```csharp
public sealed class XxxLifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly XxxModule _module;
    private readonly ILogger<XxxLifecycleService> _logger;

    public XxxLifecycleService(XxxModule module, ILogger<XxxLifecycleService> logger)
    {
        _module = module;
        _logger = logger;
    }

    public override async Task<InitializeResponse> Initialize(InitializeRequest request, ServerCallContext context) { ... }
    public override async Task<StartResponse> Start(StartRequest request, ServerCallContext context) { ... }
    public override async Task<StopResponse> Stop(StopRequest request, ServerCallContext context) { ... }
    public override async Task<HealthResponse> GetHealth(HealthRequest request, ServerCallContext context) { ... }
    public override async Task<ManifestResponse> GetManifest(ManifestRequest request, ServerCallContext context) { ... }
}
```

### 2.4 gRPC API Client in Core.Server (Contacts)

File: `src/Core/DotNetCloud.Core.Server/Grpc/Clients/ContactsGrpcApiClient.cs`

Key patterns:

1. **Options class** — per-module configuration with section name and default address:

```csharp
public sealed class ContactsGrpcClientOptions
{
    public const string SectionName = "ContactsGrpc";
    public string ContactsModuleAddress { get; set; } = "http://localhost:5002";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

2. **Implements the module's API client interface** — `IContactsApiClient` (defined in `DotNetCloud.Modules.Contacts/Services/IContactsApiClient.cs`)

3. **Lazy channel initialization** using `ModuleEndpointProvider`:

```csharp
private readonly Lazy<GrpcChannel> _channel;
private readonly Lazy<ContactsService.ContactsServiceClient> _client;

private GrpcChannel CreateChannel()
{
    var address = _endpointProvider.GetEndpoint("dotnetcloud.contacts");
    return GrpcChannel.ForAddress(address, new GrpcChannelOptions
    {
        HttpHandler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        }
    });
}
```

4. **Error handling with safe call wrappers** — `SafeCallAsync<T>`, `SafeCallListAsync<T>`, `SafeCallAsync` (void) — catch `RpcException` for `Unavailable` and `DeadlineExceeded`, log and return fallback

5. **Deadline propagation**:

```csharp
private CallOptions DeadlineHeaders(CancellationToken ct)
{
    var deadline = DateTime.UtcNow.Add(_options.Timeout);
    return new CallOptions(deadline: deadline, cancellationToken: ct);
}
```

6. **Proto ↔ DTO mapping** — `ToContactDto(ContactMessage)`, `ToCreateRequest(CreateContactDto)`, `ToUpdateRequest(Guid, UpdateContactDto)` — manual mapping between gRPC generated types and module DTOs

7. **IDisposable** — disposes the channel if created

### 2.5 Core.Server `.csproj` Changes for Isolated Module

For Contacts and Calendar (already done):

**Removed** from `<ProjectReference>`:

- `DotNetCloud.Modules.Contacts.Host` (NOT referenced)
- `DotNetCloud.Modules.Calendar.Host` (NOT referenced)

**Kept** (Data projects still needed for migrations):

- `DotNetCloud.Modules.Contacts.Data`
- `DotNetCloud.Modules.Contacts.Data.SqlServer`
- `DotNetCloud.Modules.Calendar.Data`
- `DotNetCloud.Modules.Calendar.Data.SqlServer`

**Added** gRPC client proto references:

```xml
<Protobuf Include="..\..\Modules\Contacts\DotNetCloud.Modules.Contacts.Host\Protos\contacts_service.proto"
          GrpcServices="Client"
          Link="Protos\contacts_service.proto" />
<Protobuf Include="..\..\Modules\Calendar\DotNetCloud.Modules.Calendar.Host\Protos\calendar_service.proto"
          GrpcServices="Client"
          Link="Protos\calendar_service.proto" />
```

### 2.6 Core.Server `Program.cs` Changes for Isolated Module

**Removed:**

```csharp
// builder.Services.AddContactsServices(builder.Configuration);  ← REMOVED
// builder.Services.AddCalendarServices(builder.Configuration);   ← REMOVED
```

**Added** (gRPC client registration):

```csharp
// Options binding
builder.Services.Configure<ContactsGrpcClientOptions>(
    builder.Configuration.GetSection(ContactsGrpcClientOptions.SectionName));
builder.Services.Configure<CalendarGrpcClientOptions>(
    builder.Configuration.GetSection(CalendarGrpcClientOptions.SectionName));

// Shared endpoint provider
builder.Services.AddSingleton<ModuleEndpointProvider>();

// gRPC client registration (Scoped — new channel per scope)
builder.Services.AddScoped<IContactsApiClient, ContactsGrpcApiClient>();
builder.Services.AddScoped<ICalendarApiClient, CalendarGrpcApiClient>();
```

Note: `INotesApiClient` and `ITracksApiClient` etc. are already registered in Core.Server Program.cs but with their **in-process** implementations (`NotesApiClient`, `TracksApiClient`). These need to be replaced with the gRPC implementations.

### 2.7 `manifest.json` Pattern

File: `src/Modules/Contacts/manifest.json`

```json
{
  "id": "dotnetcloud.contacts",
  "name": "Contacts",
  "version": "1.0.0",
  "description": "...",
  "author": "DotNetCloud",
  "requiredCapabilities": ["INotificationService", "IUserDirectory", "..."],
  "publishedEvents": ["ContactCreatedEvent", "..."],
  "subscribedEvents": ["CalendarEventCreatedEvent", "..."],
  "minCoreVersion": "1.0.0",
  "schemaProvider": "core"
}
```

### 2.8 Process Supervisor Discovery

File: `src/Core/DotNetCloud.Core.Server/ModuleLoading/ModuleDiscoveryService.cs`

The supervisor scans `modules/` directory for subdirectories matching the pattern:

```
modules/
├── dotnetcloud.xxx/
│   ├── dotnetcloud.xxx.dll       ← discovered as executable
│   ├── dotnetcloud.xxx.deps.json
│   ├── dotnetcloud.xxx.runtimeconfig.json
│   ├── manifest.json              ← optional but strongly recommended
│   └── appsettings.json           ← optional module config
```

The module ID is derived from the directory name. The executable is expected to be `{directory-name}.dll`. The `modules/` directory is configured via `ProcessSupervisorOptions.ModulesDirectory` (default: `modules/` relative to Core.Server content root).

File: `src/Core/DotNetCloud.Core.Server/Supervisor/ProcessSupervisor.cs`

`SpawnModuleProcess` validates that discovered paths are within the modules directory (CWE-078 prevention), then launches via `dotnet exec {path-to-dll}` with `DOTNETCLOUD_GRPC_ENDPOINT` set to a dynamically allocated port.

---

## 3. Module-Specific Technical Analysis

### 3.1 About Module (Simplest — No Database)

**Current state files:**

- `src/Modules/About/DotNetCloud.Modules.About/AboutModule.cs` — module singleton
- `src/Modules/About/DotNetCloud.Modules.About/AboutModuleManifest.cs` — code-based manifest
- `src/Modules/About/DotNetCloud.Modules.About/UI/` — Blazor components
- `src/Modules/About/DotNetCloud.Modules.About.Host/Program.cs` — maps only `AboutLifecycleService`, no REST controllers
- `src/Modules/About/DotNetCloud.Modules.About.Host/Services/AboutLifecycleService.cs` — ✓ exists
- `src/Modules/About/DotNetCloud.Modules.About.Host/Services/AboutHealthCheck.cs` — ✓ exists

**No database context, no data project, no SqlServer project.** This is the simplest module.

**API surface:** The About module provides version, system info, and license info through Blazor UI. The gRPC service needs minimal RPCs: `GetAboutInfo` (version, environment, license status). Currently served through `AboutModule` singleton injected into Blazor components.

**Core.Server references:**

- `DotNetCloud.Modules.About` (main project)
- `DotNetCloud.Modules.About.Host` (Host project — needs removal)

**Unique challenge:** About has no `AddAboutServices()` call in Core.Server Program.cs — it's purely Blazor UI. The Host project just provides the lifecycle service for health monitoring.

### 3.2 AI Module (Database-Backed)

**Current state files:**

- `src/Modules/AI/DotNetCloud.Modules.AI/AiModule.cs` — module singleton
- `src/Modules/AI/DotNetCloud.Modules.AI/AiModuleManifest.cs` — code-based manifest
- `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiChatService.cs` — business interface
- `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiSettingsProvider.cs` — settings interface
- `src/Modules/AI/DotNetCloud.Modules.AI.Data/AiDbContext.cs` — EF Core context
- `src/Modules/AI/DotNetCloud.Modules.AI.Data/AiServiceRegistration.cs` — `AddAiServices()`
- `src/Modules/AI/DotNetCloud.Modules.AI.Data.SqlServer/` — SQL Server migrations
- `src/Modules/AI/DotNetCloud.Modules.AI.Host/Controllers/AiChatController.cs` — REST API
- `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/AiHealthCheck.cs` — exists
- `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/InProcessEventBus.cs` — exists
- `src/Modules/AI/manifest.json` — ✓ exists

**API surface** (from `AiChatController`):

- `POST api/ai/conversations` — CreateConversation
- `GET api/ai/conversations/{id}` — GetConversation
- `GET api/ai/conversations` — ListConversations (needs verification)
- `POST api/ai/conversations/{id}/messages` — SendMessage + streaming response (needs verification)
- `DELETE api/ai/conversations/{id}` — DeleteConversation (needs verification)
- `GET api/ai/models` — ListAvailableModels (needs verification)
- `GET/PUT api/ai/settings` — GetSettings/UpdateSettings (needs verification)

**gRPC considerations:** AI has streaming responses (`SendMessage` returns SSE stream). gRPC supports server-streaming natively. The proto needs `rpc SendMessage (SendMessageRequest) returns (stream MessageChunk);`.

**Core.Server references:**

- `DotNetCloud.Modules.AI` (main)
- `DotNetCloud.Modules.AI.Data` (data — keep)
- `DotNetCloud.Modules.AI.Data.SqlServer` (migrations — keep)
- `DotNetCloud.Modules.AI.Host` (Host — needs removal)

### 3.3 Music Module (Host Has Hardcoded In-Memory DB)

**Current state:**

- `Program.cs` hardcodes `UseInMemoryDatabase("MusicModule")` and `PostgreSqlNamingStrategy()` — must be made config-driven
- `MapGrpcService<MusicGrpcServiceImpl>()` — ✓ mapped, but no LifecycleService
- No `DOTNETCLOUD_CONFIG_DIR` handling
- Uses `DbContextFactory<MusicDbContext>` in addition to regular `DbContext`

**Core.Server references (ALL need Host removal):**

- `DotNetCloud.Modules.Music` (Core — keep? check if needed)
- `DotNetCloud.Modules.Music.Data` (Data — keep)
- `DotNetCloud.Modules.Music.Data.SqlServer` (migrations — keep)
- `DotNetCloud.Modules.Music.Host` (Host — remove)

### 3.4 Photos Module (Host Has Hardcoded In-Memory DB)

**Current state:**

- `Program.cs` hardcodes `UseInMemoryDatabase("PhotosModule")`
- `MapGrpcService<PhotosGrpcServiceImpl>()` — ✓ mapped, no LifecycleService
- No `DOTNETCLOUD_CONFIG_DIR` handling
- No `DOTNETCLOUD_GRPC_ENDPOINT` handling

**Core.Server references:**

- `DotNetCloud.Modules.Photos` (Core — keep? check)
- `DotNetCloud.Modules.Photos.Data` (Data — keep)
- `DotNetCloud.Modules.Photos.Data.SqlServer` (migrations — keep)
- `DotNetCloud.Modules.Photos.Host` (Host — remove)

### 3.5 Video Module (Host Already Has Config-Driven DB)

**Current state:**

- `Program.cs` already reads `connectionString` and `databaseProvider` from config — most production-ready of the partial-infra modules
- `MapGrpcService<VideoGrpcServiceImpl>()` — ✓ mapped, no LifecycleService
- No `DOTNETCLOUD_CONFIG_DIR` / `DOTNETCLOUD_GRPC_ENDPOINT` handling

**Core.Server references:**

- `DotNetCloud.Modules.Video` (Core)
- `DotNetCloud.Modules.Video.Data` (Data)
- `DotNetCloud.Modules.Video.Data.SqlServer` (migrations)
- `DotNetCloud.Modules.Video.Host` (Host — remove)

### 3.6 Search Module (Has Separate Client Project)

**Current state:**

- `Program.cs` already reads env vars for database configuration via `ResolveDatabaseProvider`
- `MapGrpcService<SearchGrpcService>()` — ✓ mapped, no LifecycleService
- `DotNetCloud.Modules.Search.Client` — separate project used by Chat and Files for FTS client
- `AddSearchFtsClient()` used by Chat.Host and Files.Host via `DotNetCloud.Modules.Search.Client` package
- `ISearchableModule` registrations in Core.Server reference module Data projects directly — needs gRPC refactoring

**Core.Server references:**

- `DotNetCloud.Modules.Search` (Core)
- `DotNetCloud.Modules.Search.Data` (Data)
- `DotNetCloud.Modules.Search.Data.SqlServer` (migrations)
- `DotNetCloud.Modules.Search.Host` (Host — remove)

### 3.7 Files Module (Most Complex — Storage, Collabora, Shared Folders)

**Current state:**

- `Program.cs` already handles `DOTNETCLOUD_DATA_DIR` for storage path
- `MapGrpcService<FilesGrpcService>()` + `MapGrpcService<FilesLifecycleService>()` — both mapped
- Has `IFileStorageEngine` (local filesystem) — needs to be configured from shared config when process-isolated
- Has `AddSearchFtsClient()` for Search module gRPC
- Has `DeviceIdentityFilter` for upload authentication
- Has OpenAPI/Scalar docs

**Core.Server has Files-specific wiring:**

- `filesStoragePath` resolution from `Files:StoragePath` or `DOTNETCLOUD_DATA_DIR`
- `IFileStorageEngine` as `LocalFileStorageEngine`
- `IFileValidationService` singleton
- `DeviceIdentityFilter` on controllers
- `InProcessAdminSharedFolderReindexDispatcher` — directly instantiates Search module service
- `UserOrganizationResolver` as `IUserOrganizationResolver`

**This is the hardest module. After removing Host reference, Core.Server must:**

- Keep `IFileStorageEngine` registration (storage is a core concern, not a module concern — the Files module queries files, the core manages storage)
- Refactor `InProcessAdminSharedFolderReindexDispatcher` to use gRPC
- Ensure `DeviceIdentityFilter` is available (it's in `DotNetCloud.Modules.Files`, which stays referenced)

### 3.8 Tracks Module

**Core.Server references:**

- `DotNetCloud.Modules.Tracks` (Core — contains `ITracksApiClient`, `IOnboardingStateService`)
- `DotNetCloud.Modules.Tracks.Data` (Data)
- `DotNetCloud.Modules.Tracks.Data.SqlServer` (migrations)
- `DotNetCloud.Modules.Tracks.Host` (Host — remove)

**Core.Server Program.cs already registers:**

- `ITracksApiClient` → `TracksApiClient` (in-process — must change to `TracksGrpcApiClient`)
- `IOnboardingStateService` → `OnboardingStateService` (in-process — evaluate if this needs gRPC or can be internal to module)

### 3.9 Chat, Notes, Bookmarks, Email Modules

These follow the standard pattern. Chat and Notes have `AddSearchFtsClient()`. All need Host Program.cs updated with `DOTNETCLOUD_CONFIG_DIR` / `DOTNETCLOUD_GRPC_ENDPOINT` handling and `manifest.json` created.

---

## 4. Core.Server Current In-Process Registrations (Full Inventory)

File: `src/Core/DotNetCloud.Core.Server/Program.cs` (lines ~305-330)

Currently registered in-process (ALL must be removed or converted):

```csharp
builder.Services.AddFilesServices(builder.Configuration);       // → FilesGrpcApiClient
builder.Services.AddChatServices(builder.Configuration);        // → ChatGrpcApiClient
builder.Services.AddNotesServices(builder.Configuration);       // → NotesGrpcApiClient
builder.Services.AddTracksServices(builder.Configuration);      // → TracksGrpcApiClient
builder.Services.AddPhotosServices(builder.Configuration);      // → PhotosGrpcApiClient
builder.Services.AddMusicServices(builder.Configuration);       // → MusicGrpcApiClient
builder.Services.AddVideoServices(builder.Configuration);       // → VideoGrpcApiClient
builder.Services.AddAiServices(builder.Configuration);          // → AiGrpcApiClient
builder.Services.AddSearchServices(builder.Configuration);      // → SearchGrpcApiClient
builder.Services.AddBookmarksServices(builder.Configuration);   // → BookmarksGrpcApiClient
builder.Services.AddEmailServices(builder.Configuration);       // → EmailGrpcApiClient
```

Also in-process (needs evaluation):

```csharp
// These reference module types directly and must be refactored:
builder.Services.AddScoped<ISearchableModule, FilesSearchableModule>();
builder.Services.AddScoped<ISearchableModule, NotesSearchableModule>();
builder.Services.AddScoped<ISearchableModule, CalendarSearchableModule>();
builder.Services.AddScoped<ISearchableModule, BookmarksSearchableModule>();
builder.Services.AddScoped<ISearchableModule, EmailSearchableModule>();

builder.Services.AddScoped<INotesApiClient, NotesApiClient>();        // → NotesGrpcApiClient
builder.Services.AddScoped<ITracksApiClient, TracksApiClient>();      // → TracksGrpcApiClient
builder.Services.AddScoped<IEmailApiClient, EmailApiClient>();        // → EmailGrpcApiClient
builder.Services.AddScoped<IBookmarksApiClient, BookmarksApiClient>(); // → BookmarksGrpcApiClient
```

Cross-module wiring (must be refactored):

```csharp
builder.Services.AddSingleton<IAdminSharedFolderReindexDispatcher>(sp =>
    new InProcessAdminSharedFolderReindexDispatcher(
        sp.GetService<SearchReindexBackgroundService>()));
// ↑ Direct reference to Search module — must become gRPC call
```

### 4.1 `AddModuleDbContexts` (KEEP — Does NOT Need Changing)

File: `src/Core/DotNetCloud.Core.Server/Extensions/ModuleServiceRegistrationExtensions.cs`

This method registers all module `DbContext` types in the Core.Server DI container for migration application and schema management. All 14 modules' Data projects are referenced directly — this is correct and must NOT change. Module Data projects are NOT module Host projects; they're class libraries providing EF Core models. The Core.Server needs these for:

- Applying EF Core migrations at startup
- Schema creation (`IModuleSchemaProvider`)
- The `ModuleDiscoveryService` which queries the `ModuleRegistrations` table

---

## 5. Implementation Phases

### Phase 1: Documentation — Mandate gRPC (DO FIRST)

#### Step 1.1: Update `docs/architecture/ARCHITECTURE.md`

In Section 2 ("Architecture Pattern"), after the "Process Communication" table, add a new subsection:

**"Inter-Module Communication Policy (MANDATORY)"**

- All inter-module communication MUST use gRPC exclusively
- Modules MUST NOT reference each other's Host projects as `<ProjectReference>`
- Modules MUST NOT resolve each other's services from the DI container
- Modules MUST NOT access each other's databases directly
- The ONLY allowed cross-module communication is:
  - gRPC calls (for request/response operations)
  - Event bus messages (for pub/sub notifications — event bus itself relays via gRPC)
- Violation is a HARD BLOCKER for PR approval
- First-party modules use the same gRPC interface as third-party modules (dogfooding)

Add a callout box (blockquote with ⚠️):

> ⚠️ **ENFORCEMENT:** Any module that communicates with another module via direct in-process calls, shared DI, or direct database access will be rejected in code review. This is not a guideline — it is a hard architectural requirement. The Contacts → Calendar cross-module gRPC pattern (Calendar.Host uses ContactsGrpcClient to call Contacts module) is the ONLY acceptable pattern.

#### Step 1.2: Update `docs/guides/MODULE_DEVELOPMENT.md`

In Section 9 ("gRPC Communication"), add at the top:

**"⚠️ MANDATORY: gRPC is the ONLY allowed inter-module communication mechanism."**

Add a mandatory checklist for every module:

> **Every module MUST have:**
>
> - ☐ A `.proto` file in `Host/Protos/` defining all RPCs
> - ☐ A `GrpcService` class implementing the generated base class
> - ☐ A `LifecycleService` class extending `ModuleLifecycle.ModuleLifecycleBase`
> - ☐ A `manifest.json` at the module root declaring capabilities and events
> - ☐ A gRPC API client interface (`IXxxApiClient`) in the module's main project
> - ☐ A gRPC API client implementation (`XxxGrpcApiClient`) in `Core.Server/Grpc/Clients/`
> - ☐ A `Program.cs` in the Host project that handles `DOTNETCLOUD_CONFIG_DIR` and `DOTNETCLOUD_GRPC_ENDPOINT`
> - ☐ NO direct `<ProjectReference>` from `Core.Server.csproj` to the module's Host project
> - ☐ `AssemblyName` in Host `.csproj` set to `dotnetcloud.{module-id}`

Add a new section "Converting an In-Process Module to Process-Isolated gRPC" that documents the Contacts conversion as the canonical example. Include:

- The 5 changes to Core.Server (csproj, Program.cs, new gRPC client, config, removal)
- The 5 changes to the module Host (csproj, Program.cs, LifecycleService, manifest.json, proto)
- The deployment script update

#### Step 1.3: Update Instruction Files

**`CLAUDE.md`** — add under "Architecture" section:

```markdown
### gRPC Communication (MANDATORY)

All current and future modules MUST run as process-isolated processes communicating
exclusively via gRPC. Direct in-process calls between modules, shared DI container
references, and direct cross-module database access are FORBIDDEN. This is enforced
in code review.
```

**`.github/copilot-instructions.md`** — add a new section under the architecture rules:

```markdown
## 🚨 CRITICAL: gRPC-Only Inter-Module Communication (MANDATORY)

ALL modules MUST communicate exclusively via gRPC. No exceptions.

**Forbidden patterns (will be rejected):**

- ❌ `<ProjectReference>` from Core.Server.csproj to any module's `.Host` project
- ❌ `builder.Services.AddXxxServices()` in Core.Server/Program.cs
- ❌ Direct instantiation of module services in Core.Server
- ❌ Cross-module DI resolution

**Required pattern (only acceptable approach):**

- ✅ gRPC proto definitions in module Host project
- ✅ gRPC client in Core.Server/Grpc/Clients/ implementing IXxxApiClient
- ✅ Process-isolated module host launched by ProcessSupervisor
- ✅ All inter-module calls go through gRPC over Unix sockets/Named Pipes
```

#### Step 1.4: Update `docs/IMPLEMENTATION_CHECKLIST.md`

Add a new Phase section "Phase 0.7: gRPC Conversion Enforcement" with checkboxes.

---

### Phase 2: About + AI — Greenfield gRPC (can run in parallel)

#### Step 2.1: About Module

**2.1.1** Create `src/Modules/About/DotNetCloud.Modules.About.Host/Protos/about_service.proto`:

```protobuf
syntax = "proto3";
option csharp_namespace = "DotNetCloud.Modules.About.Host.Protos";
package dotnetcloud.about;

service AboutService {
  rpc GetAboutInfo (GetAboutInfoRequest) returns (AboutInfoResponse);
}

message GetAboutInfoRequest { string user_id = 1; }
message AboutInfoResponse {
  bool success = 1;
  string error_message = 2;
  string version = 3;
  string environment = 4;
  string runtime_version = 5;
  string os_description = 6;
  string license_status = 7;
  string uptime = 8;
}
```

**2.1.2** Create `src/Modules/About/DotNetCloud.Modules.About.Host/Services/AboutGrpcService.cs`:

- Implements `AboutService.AboutServiceBase`
- Injects `AboutModule`
- Maps `AboutModule` properties to `AboutInfoResponse`

**2.1.3** Update `src/Modules/About/DotNetCloud.Modules.About.Host/Program.cs`:

- Add `DOTNETCLOUD_CONFIG_DIR` / `DOTNETCLOUD_GRPC_ENDPOINT` handling (standard pattern)
- Add `app.MapGrpcService<AboutGrpcService>()` (AboutLifecycleService already mapped)

**2.1.4** Update `src/Modules/About/DotNetCloud.Modules.About.Host/DotNetCloud.Modules.About.Host.csproj`:

- Add `<AssemblyName>dotnetcloud.about</AssemblyName>`
- Add `<PublishWithPackageReferences>false</PublishWithPackageReferences>`
- Add `<PublishReadyToCompile>false</PublishReadyToCompile>`
- Add `<Protobuf Include="Protos\about_service.proto" GrpcServices="Server" />`

**2.1.5** Create `src/Modules/About/manifest.json`

**2.1.6** Create `src/Modules/About/DotNetCloud.Modules.About/Services/IAboutApiClient.cs`

**2.1.7** Create `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AboutGrpcApiClient.cs`

**2.1.8** Update `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`:

- Add gRPC client proto reference
- Remove About.Host ProjectReference

**2.1.9** Update `src/Core/DotNetCloud.Core.Server/Program.cs`:

- Add options binding and client registration
- Note: No `AddAboutServices()` to remove

#### Step 2.2: AI Module

**2.2.1** Examine full `AiChatController.cs` to determine complete RPC surface.

**2.2.2** Create `src/Modules/AI/DotNetCloud.Modules.AI.Host/Protos/ai_service.proto` with all RPCs including server-streaming `SendMessage`.

**2.2.3** Create `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/AiGrpcService.cs`

**2.2.4** Create `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/AiLifecycleService.cs`

**2.2.5** Update `src/Modules/AI/DotNetCloud.Modules.AI.Host/Program.cs` with env var handling + config-driven DB

**2.2.6** Update `src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj` with AssemblyName, Protobuf, publish settings, OpenIddict

**2.2.7** Verify `src/Modules/AI/manifest.json`

**2.2.8** Create `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiApiClient.cs`

**2.2.9** Create `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AiGrpcApiClient.cs` with streaming support

**2.2.10** Update Core.Server `.csproj` and `Program.cs`

---

### Phase 3: Partial-Infra Modules — LifecycleService + gRPC Client (can run in parallel)

Each module in this phase needs the same 7 operations. The pattern is identical across Music, Photos, Video, and Search.

#### Operations per module:

1. Create `XxxLifecycleService.cs`
2. Update `Program.cs` with config env vars + config-driven DB + LifecycleService mapping
3. Update `.csproj` with publish settings + OpenIddict
4. Create `manifest.json`
5. Create `IXxxApiClient.cs` in module's main project
6. Create `XxxGrpcApiClient.cs` in Core.Server/Grpc/Clients/
7. Update Core.Server: add proto/client, remove Host ref + `AddXxxServices()`

All four modules can be done in parallel.

---

### Phase 4: Full-Infra Modules — gRPC Client Only (can run in parallel)

These modules already have proto + GrpcService + LifecycleService. Only need the gRPC client in Core.Server and Host Program.cs hardening.

#### Common Operations per module:

1. Create `IXxxApiClient` interface (if not already present)
2. Create `XxxGrpcApiClient.cs` in Core.Server/Grpc/Clients/
3. Update module Host `Program.cs` with `DOTNETCLOUD_CONFIG_DIR` and `DOTNETCLOUD_GRPC_ENDPOINT`
4. Create `manifest.json` at module root
5. Update Core.Server `.csproj`: add gRPC client proto, remove Host ProjectReference
6. Update Core.Server `Program.cs`: add options + client registration, remove `AddXxxServices()`

**Step 4.1:** Chat Module  
**Step 4.2:** Files Module (MOST COMPLEX — see analysis above)  
**Step 4.3:** Notes Module  
**Step 4.4:** Tracks Module  
**Step 4.5:** Bookmarks Module  
**Step 4.6:** Email Module

---

### Phase 5: Deployment & Build Integration (depends on ALL conversions)

**Step 5.1:** Update `scripts/publish-module-hosts.ps1` — add all 14 modules  
**Step 5.2:** Update `scripts/deploy.sh` — add all 14 modules to publish loop  
**Step 5.3:** Verify `ModuleDiscoveryService` can discover all modules  
**Step 5.4:** Verify `DotNetCloud.CI.slnf` includes all needed projects

---

### Phase 6: Cross-Cutting Concerns Cleanup (depends on Phase 4)

**Step 6.1:** `ISearchableModule` refactoring — replace direct DI with gRPC-based search indexing  
**Step 6.2:** `InProcessAdminSharedFolderReindexDispatcher` — refactor to gRPC  
**Step 6.3:** `InProcessEventBus` in Core.Server — evaluate/replace (can be deferred)  
**Step 6.4:** `CrossModuleLinkResolver` — refactor to gRPC  
**Step 6.5:** `UserOrganizationResolver` — refactor to gRPC  
**Step 6.6:** Verify `AddModuleDbContexts` is unaffected (confirmed: no changes needed)

---

### Phase 7: Verification & Testing

**Step 7.1:** Build verification — `dotnet build DotNetCloud.CI.slnf -c Release`  
**Step 7.2:** Proto compilation verification  
**Step 7.3:** Publish verification — all 14 module directories created  
**Step 7.4:** Test suite — `dotnet test`  
**Step 7.5:** Runtime verification — end-to-end operations across all modules

---

## 6. Risk Analysis & Edge Cases

### High Risk

1. **Files module:** Most cross-cutting concerns. Needs dedicated sub-plan.
2. **Search module:** `ISearchableModule` refactoring requires coordinated proto changes across 5 modules.
3. **Streaming RPCs (AI):** `SendMessage` needs server-streaming gRPC with `IAsyncEnumerable`.

### Medium Risk

4. **Module host Program.cs hardening:** Config-driven DB with in-memory fallback.
5. **Proto message versioning:** Adding `GetSearchableDocuments` RPC to modules that lack it.
6. **Test failures:** Tests with direct module DI will break.

### Low Risk

7. **About module:** No database, minimal API.
8. **Deployment scripts:** Straightforward additions.
9. **Documentation:** Purely additive.

---

## 7. Key Files Index

### Files to CREATE (42 new files)

| File                                                                                    | Purpose                     |
| --------------------------------------------------------------------------------------- | --------------------------- |
| `src/Modules/About/DotNetCloud.Modules.About.Host/Protos/about_service.proto`           | About proto                 |
| `src/Modules/About/DotNetCloud.Modules.About.Host/Services/AboutGrpcService.cs`         | About gRPC service          |
| `src/Modules/About/DotNetCloud.Modules.About/Services/IAboutApiClient.cs`               | About API client interface  |
| `src/Modules/About/manifest.json`                                                       | About manifest              |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AboutGrpcApiClient.cs`                   | About gRPC client           |
| `src/Modules/AI/DotNetCloud.Modules.AI.Host/Protos/ai_service.proto`                    | AI proto                    |
| `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/AiGrpcService.cs`                  | AI gRPC service             |
| `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/AiLifecycleService.cs`             | AI lifecycle                |
| `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiApiClient.cs`                        | AI API client interface     |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AiGrpcApiClient.cs`                      | AI gRPC client              |
| `src/Modules/Music/DotNetCloud.Modules.Music.Host/Services/MusicLifecycleService.cs`    | Music lifecycle             |
| `src/Modules/Music/DotNetCloud.Modules.Music/Services/IMusicApiClient.cs`               | Music API client interface  |
| `src/Modules/Music/manifest.json`                                                       | Music manifest              |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/MusicGrpcApiClient.cs`                   | Music gRPC client           |
| `src/Modules/Photos/DotNetCloud.Modules.Photos.Host/Services/PhotosLifecycleService.cs` | Photos lifecycle            |
| `src/Modules/Photos/DotNetCloud.Modules.Photos/Services/IPhotosApiClient.cs`            | Photos API client interface |
| `src/Modules/Photos/manifest.json`                                                      | Photos manifest             |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/PhotosGrpcApiClient.cs`                  | Photos gRPC client          |
| `src/Modules/Video/DotNetCloud.Modules.Video.Host/Services/VideoLifecycleService.cs`    | Video lifecycle             |
| `src/Modules/Video/DotNetCloud.Modules.Video/Services/IVideoApiClient.cs`               | Video API client interface  |
| `src/Modules/Video/manifest.json`                                                       | Video manifest              |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/VideoGrpcApiClient.cs`                   | Video gRPC client           |
| `src/Modules/Search/DotNetCloud.Modules.Search.Host/Services/SearchLifecycleService.cs` | Search lifecycle            |
| `src/Modules/Search/DotNetCloud.Modules.Search/Services/ISearchApiClient.cs`            | Search API client interface |
| `src/Modules/Search/manifest.json`                                                      | Search manifest             |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/SearchGrpcApiClient.cs`                  | Search gRPC client          |
| `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/IChatApiClient.cs`                  | Chat API client interface   |
| `src/Modules/Chat/manifest.json`                                                        | Chat manifest               |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/ChatGrpcApiClient.cs`                    | Chat gRPC client            |
| `src/Modules/Files/DotNetCloud.Modules.Files/Services/IFilesApiClient.cs`               | Files API client interface  |
| `src/Modules/Files/manifest.json`                                                       | Files manifest              |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/FilesGrpcApiClient.cs`                   | Files gRPC client           |
| `src/Modules/Tracks/manifest.json`                                                      | Tracks manifest             |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/TracksGrpcApiClient.cs`                  | Tracks gRPC client          |
| `src/Modules/Bookmarks/manifest.json`                                                   | Bookmarks manifest          |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/BookmarksGrpcApiClient.cs`               | Bookmarks gRPC client       |
| `src/Modules/Email/manifest.json`                                                       | Email manifest              |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/EmailGrpcApiClient.cs`                   | Email gRPC client           |

### Files to MODIFY (30 files)

See the full plan for the detailed per-file change list.

---

## 8. Decisions & Assumptions

1. **Pattern:** Follow Contacts/Calendar conversion exactly. No new patterns, no shortcuts.
2. **About first:** Simplest module (no database). Validates the workflow.
3. **Files last in Phase 4:** Most complex. Tackle after other full-infra modules.
4. **Data projects stay:** All module Data projects remain as Core.Server ProjectReferences.
5. **Core projects stay:** `DotNetCloud.Modules.Xxx` (main) stays referenced.
6. **Only Host projects removed:** The ONLY ProjectReferences removed are `.Host` projects.
7. **Port allocation:** Default ports follow `5000 + index` pattern.
8. **ISearchableModule refactoring:** Flagged as significant work, may need dedicated follow-up.
9. **InProcessEventBus in Core.Server:** Keep for now. Defer module-to-module event relay.
10. **Documentation is non-negotiable:** gRPC mandate must be enforced in code review.

---

## 9. Verification Checklist (Post-Implementation)

- [ ] `dotnet build DotNetCloud.CI.slnf -c Release` passes (0 errors, 0 warnings)
- [ ] `dotnet test` passes (all tests green)
- [ ] No `.Host` ProjectReferences remain in `Core.Server.csproj`
- [ ] No `AddXxxServices()` calls remain in `Core.Server/Program.cs`
- [ ] `modules/` directory contains 14 subdirectories after publish
- [ ] Each module directory has: `.dll`, `.deps.json`, `.runtimeconfig.json`, `manifest.json`
- [ ] ProcessSupervisor discovers and launches all 14 modules
- [ ] Health endpoint returns healthy for all modules
- [ ] File upload/download works (Files via gRPC)
- [ ] Chat message send/receive works (Chat via gRPC)
- [ ] Calendar event CRUD works (Calendar via gRPC)
- [ ] Contact CRUD works (Contacts via gRPC)
- [ ] Full-text search returns results across modules (Search via gRPC)
- [ ] AI chat streaming works (AI via gRPC server-streaming)
- [ ] About system info accessible (About via gRPC)
- [ ] All documentation files updated with gRPC mandate

---

## 10. Implementation Reference — Complete File Contents

> **⚠️ README:** This section contains the COMPLETE, compilable contents of every file that needs to be created or replaced. A lesser LLM can copy-paste these verbatim. Files are marked with `// COMPLETE FILE`.

### 10.1 LifecycleService Template

For Music, Photos, Video, Search, and AI modules. Replace `{Module}` (e.g., `Music`, `Photos`, `Video`, `Search`, `Ai`).

**Path:** `src/Modules/{Module}/DotNetCloud.Modules.{Module}.Host/Services/{Module}LifecycleService.cs`

```csharp
// COMPLETE FILE
using DotNetCloud.Core.Grpc.Lifecycle;
using DotNetCloud.Core.Modules;
using Grpc.Core;

namespace DotNetCloud.Modules.{Module}.Host.Services;

public sealed class {Module}LifecycleService : ModuleLifecycle.ModuleLifecycleBase
{
    private readonly {Module}Module _module;
    private readonly ILogger<{Module}LifecycleService> _logger;

    public {Module}LifecycleService({Module}Module module, ILogger<{Module}LifecycleService> logger)
    {
        _module = module;
        _logger = logger;
    }

    public override async Task<InitializeResponse> Initialize(InitializeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Initializing {Module} module via gRPC: {ModuleId}", request.ModuleId);
            var config = request.Configuration.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            var initContext = new ModuleInitializationContext
            {
                ModuleId = request.ModuleId,
                Services = context.GetHttpContext().RequestServices,
                Configuration = config,
                SystemCaller = Core.Authorization.CallerContext.CreateSystemContext()
            };
            await _module.InitializeAsync(initContext, context.CancellationToken);
            return new InitializeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize {Module} module");
            return new InitializeResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public override async Task<StartResponse> Start(StartRequest request, ServerCallContext context)
    {
        try { await _module.StartAsync(context.CancellationToken); return new StartResponse { Success = true }; }
        catch (Exception ex) { _logger.LogError(ex, "Failed to start {Module} module"); return new StartResponse { Success = false, ErrorMessage = ex.Message }; }
    }

    public override async Task<StopResponse> Stop(StopRequest request, ServerCallContext context)
    {
        try { await _module.StopAsync(context.CancellationToken); return new StopResponse { Success = true }; }
        catch (Exception ex) { _logger.LogError(ex, "Failed to stop {Module} module"); return new StopResponse { Success = false, ErrorMessage = ex.Message }; }
    }

    public override Task<HealthCheckResponse> HealthCheck(HealthCheckRequest request, ServerCallContext context)
    {
        var response = new HealthCheckResponse { Status = HealthStatus.Healthy, Description = "{Module} module is healthy" };
        response.Metadata.Add("module_id", _module.Manifest.Id);
        response.Metadata.Add("version", _module.Manifest.Version);
        return Task.FromResult(response);
    }

    public override Task<ManifestResponse> GetManifest(ManifestRequest request, ServerCallContext context)
    {
        var m = _module.Manifest;
        var response = new ManifestResponse { Id = m.Id, Name = m.Name, Version = m.Version };
        response.Capabilities.AddRange(m.RequiredCapabilities ?? []);
        response.PublishedEvents.AddRange(m.PublishedEvents ?? []);
        response.SubscribedEvents.AddRange(m.SubscribedEvents ?? []);
        return Task.FromResult(response);
    }
}
```

### 10.2 gRPC API Client Infrastructure Reference

Every gRPC client follows this pattern. See the ContactsGrpcApiClient.cs (Section 2.4) for the complete reference. Key per-module differences:

| Module    | ModuleId              | Port | Options Section | ProtoServiceType                          |
| --------- | --------------------- | ---- | --------------- | ----------------------------------------- |
| Music     | dotnetcloud.music     | 5005 | MusicGrpc       | MusicGrpcService.MusicGrpcServiceClient   |
| Photos    | dotnetcloud.photos    | 5006 | PhotosGrpc      | PhotosGrpcService.PhotosGrpcServiceClient |
| Video     | dotnetcloud.video     | 5007 | VideoGrpc       | VideoGrpcService.VideoGrpcServiceClient   |
| Search    | dotnetcloud.search    | 5008 | SearchGrpc      | SearchService.SearchServiceClient         |
| Chat      | dotnetcloud.chat      | 5009 | ChatGrpc        | ChatService.ChatServiceClient             |
| Files     | dotnetcloud.files     | 5004 | FilesGrpc       | FilesService.FilesServiceClient           |
| Notes     | dotnetcloud.notes     | 5010 | NotesGrpc       | NotesGrpcService.NotesGrpcServiceClient   |
| Tracks    | dotnetcloud.tracks    | 5011 | TracksGrpc      | TracksGrpcService.TracksGrpcServiceClient |
| Bookmarks | dotnetcloud.bookmarks | 5012 | BookmarksGrpc   | BookmarksService.BookmarksServiceClient   |
| Email     | dotnetcloud.email     | 5013 | EmailGrpc       | EmailService.EmailServiceClient           |
| About     | dotnetcloud.about     | 5014 | AboutGrpc       | AboutService.AboutServiceClient           |
| AI        | dotnetcloud.ai        | 5015 | AiGrpc          | AiService.AiServiceClient                 |

RPC method pattern:

```csharp
// Unary
public async Task<FooDto?> GetFooAsync(Guid id, CancellationToken ct = default)
    => await SafeCallAsync(async () =>
    {
        var req = new GetFooRequest { Id = id.ToString(), UserId = Guid.Empty.ToString() };
        var resp = await _client.Value.GetFooAsync(req, DeadlineHeaders(ct)).ResponseAsync;
        return resp.Success ? MapToDto(resp.Foo) : null;
    }, "GetFoo");

// List
public async Task<IReadOnlyList<FooDto>> ListFoosAsync(CancellationToken ct = default)
    => (await SafeCallListAsync(async () =>
    {
        var req = new ListFoosRequest { UserId = Guid.Empty.ToString() };
        var resp = await _client.Value.ListFoosAsync(req, DeadlineHeaders(ct)).ResponseAsync;
        return !resp.Success ? [] : resp.Foos.Select(MapToDto).Where(d => d is not null).Select(d => d!).ToList();
    }, "ListFoos", Array.Empty<FooDto>()))!;
```

### 10.3 About Module — Complete Files

#### 10.3.1 `src/Modules/About/DotNetCloud.Modules.About.Host/Protos/about_service.proto`

```protobuf
// COMPLETE FILE
syntax = "proto3";
option csharp_namespace = "DotNetCloud.Modules.About.Host.Protos";
package dotnetcloud.about;

service AboutService { rpc GetAboutInfo (GetAboutInfoRequest) returns (AboutInfoResponse); }
message GetAboutInfoRequest { string user_id = 1; }
message AboutInfoResponse {
  bool success = 1; string error_message = 2; string version = 3; string environment = 4;
  string runtime_version = 5; string os_description = 6; string license_status = 7; string uptime = 8;
}
```

#### 10.3.2 `src/Modules/About/DotNetCloud.Modules.About.Host/Services/AboutGrpcService.cs`

```csharp
// COMPLETE FILE
using DotNetCloud.Modules.About.Host.Protos;
using Grpc.Core;

namespace DotNetCloud.Modules.About.Host.Services;

public sealed class AboutGrpcService : AboutService.AboutServiceBase
{
    private readonly AboutModule _module;
    private readonly ILogger<AboutGrpcService> _logger;

    public AboutGrpcService(AboutModule module, ILogger<AboutGrpcService> logger) { _module = module; _logger = logger; }

    public override Task<AboutInfoResponse> GetAboutInfo(GetAboutInfoRequest request, ServerCallContext context)
    {
        try
        {
            return Task.FromResult(new AboutInfoResponse
            {
                Success = true, Version = _module.Manifest.Version,
                Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                RuntimeVersion = System.Environment.Version.ToString(),
                OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                LicenseStatus = "MIT", Uptime = System.Environment.TickCount64.ToString()
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAboutInfo failed"); return Task.FromResult(new AboutInfoResponse { Success = false, ErrorMessage = ex.Message }); }
    }
}
```

#### 10.3.3 `src/Modules/About/DotNetCloud.Modules.About/Services/IAboutApiClient.cs`

```csharp
// COMPLETE FILE
namespace DotNetCloud.Modules.About.Services;

public interface IAboutApiClient { Task<AboutInfoDto?> GetAboutInfoAsync(CancellationToken ct = default); }
public sealed record AboutInfoDto { public string Version { get; init; } = ""; public string Environment { get; init; } = ""; public string RuntimeVersion { get; init; } = ""; public string OsDescription { get; init; } = ""; public string LicenseStatus { get; init; } = ""; public string Uptime { get; init; } = ""; }
```

#### 10.3.4 `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AboutGrpcApiClient.cs`

```csharp
// COMPLETE FILE
using DotNetCloud.Modules.About.Host.Protos;
using DotNetCloud.Modules.About.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

public sealed class AboutGrpcClientOptions { public const string SectionName = "AboutGrpc"; public string AboutModuleAddress { get; set; } = "http://localhost:5014"; public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30); }

public sealed class AboutGrpcApiClient : IAboutApiClient, IDisposable
{
    private readonly AboutGrpcClientOptions _options;
    private readonly ModuleEndpointProvider _endpointProvider;
    private readonly ILogger<AboutGrpcApiClient> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<AboutService.AboutServiceClient> _client;
    private bool _disposed;

    public AboutGrpcApiClient(IOptions<AboutGrpcClientOptions> options, ModuleEndpointProvider endpointProvider, ILogger<AboutGrpcApiClient> logger)
    {
        _options = options.Value; _endpointProvider = endpointProvider; _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<AboutService.AboutServiceClient>(() => new AboutService.AboutServiceClient(_channel.Value));
    }

    public async Task<AboutInfoDto?> GetAboutInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _client.Value.GetAboutInfoAsync(new GetAboutInfoRequest { UserId = Guid.Empty.ToString() }, DeadlineHeaders(ct)).ResponseAsync;
            return resp.Success ? new AboutInfoDto { Version = resp.Version, Environment = resp.Environment, RuntimeVersion = resp.RuntimeVersion, OsDescription = resp.OsDescription, LicenseStatus = resp.LicenseStatus, Uptime = resp.Uptime } : null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable) { _logger.LogWarning("About gRPC unavailable"); return null; }
        catch (Exception ex) { _logger.LogError(ex, "About gRPC error"); return null; }
    }

    private CallOptions DeadlineHeaders(CancellationToken ct) => new(deadline: DateTime.UtcNow.Add(_options.Timeout), cancellationToken: ct);
    private GrpcChannel CreateChannel()
    {
        var address = _endpointProvider.GetEndpoint("dotnetcloud.about");
        _logger.LogInformation("AboutGrpcApiClient connecting to {Address}", address);
        return GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true, ConnectTimeout = TimeSpan.FromSeconds(5) } });
    }
    public void Dispose() { if (!_disposed) { _disposed = true; if (_channel.IsValueCreated) try { _channel.Value.Dispose(); } catch { } } }
}
```

#### 10.3.5 `src/Modules/About/manifest.json`

```json
{
  "id": "dotnetcloud.about",
  "name": "About",
  "version": "1.0.0",
  "description": "System information, version, and license status.",
  "author": "DotNetCloud",
  "requiredCapabilities": [],
  "publishedEvents": [],
  "subscribedEvents": [],
  "minCoreVersion": "1.0.0"
}
```

#### 10.3.6 Updated `src/Modules/About/DotNetCloud.Modules.About.Host/Program.cs` — REPLACE ENTIRE FILE

```csharp
// COMPLETE FILE
using DotNetCloud.Modules.About;
using DotNetCloud.Modules.About.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir)) {
    var p = Path.Combine(configDir, "config.json");
    if (File.Exists(p)) builder.Configuration.AddJsonFile(p, optional: true, reloadOnChange: false);
}
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if (!string.IsNullOrEmpty(grpcEndpoint)) {
    var uri = new Uri(grpcEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://"));
    builder.WebHost.ConfigureKestrel(o => o.Listen(System.Net.IPAddress.Loopback, uri.Port, l => l.Protocols = HttpProtocols.Http2));
}
builder.Services.AddSingleton<AboutModule>();
builder.Services.AddGrpc();
builder.Services.AddHealthChecks().AddCheck<AboutHealthCheck>("about_module");
var app = builder.Build();
app.MapGrpcService<AboutGrpcService>();
app.MapGrpcService<AboutLifecycleService>();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.about", version = "1.0.0", status = "running" }));
await app.RunAsync();
```

#### 10.3.7 Updated `src/Modules/About/DotNetCloud.Modules.About.Host/DotNetCloud.Modules.About.Host.csproj` — REPLACE ENTIRE FILE

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DotNetCloud.Modules.About.Host</RootNamespace><AssemblyName>dotnetcloud.about</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PublishWithPackageReferences>false</PublishWithPackageReferences><PublishReadyToCompile>false</PublishReadyToCompile>
  </PropertyGroup>
  <ItemGroup><PackageReference Include="Grpc.AspNetCore" /></ItemGroup>
  <ItemGroup><Protobuf Include="Protos\about_service.proto" GrpcServices="Server" /></ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DotNetCloud.Modules.About\DotNetCloud.Modules.About.csproj" />
    <ProjectReference Include="..\..\..\Core\DotNetCloud.Core.Grpc\DotNetCloud.Core.Grpc.csproj" />
  </ItemGroup>
</Project>
```

### 10.4 AI Module — Complete Files

#### 10.4.1 `src/Modules/AI/DotNetCloud.Modules.AI.Host/Protos/ai_service.proto`

```protobuf
// COMPLETE FILE
syntax = "proto3";
option csharp_namespace = "DotNetCloud.Modules.AI.Host.Protos";
package dotnetcloud.ai;

service AiService {
  rpc CreateConversation (CreateConversationRequest) returns (ConversationResponse);
  rpc GetConversation (GetConversationRequest) returns (ConversationResponse);
  rpc ListConversations (ListConversationsRequest) returns (ListConversationsResponse);
  rpc DeleteConversation (DeleteConversationRequest) returns (DeleteConversationResponse);
  rpc RenameConversation (RenameConversationRequest) returns (ConversationResponse);
  rpc SendMessage (SendMessageRequest) returns (ChatResponse);
  rpc SendMessageStream (SendMessageRequest) returns (stream MessageChunk);
  rpc ListModels (ListModelsRequest) returns (ListModelsResponse);
  rpc GetSettings (GetSettingsRequest) returns (SettingsResponse);
  rpc UpdateSettings (UpdateSettingsRequest) returns (SettingsResponse);
}
message CreateConversationRequest { string user_id=1; string title=2; string model=3; string system_prompt=4; }
message GetConversationRequest { string conversation_id=1; string user_id=2; }
message ListConversationsRequest { string user_id=1; }
message DeleteConversationRequest { string conversation_id=1; string user_id=2; }
message RenameConversationRequest { string conversation_id=1; string user_id=2; string new_title=3; }
message SendMessageRequest { string conversation_id=1; string user_id=2; string message=3; }
message ListModelsRequest { string user_id=1; }
message GetSettingsRequest { string user_id=1; }
message UpdateSettingsRequest { string user_id=1; string provider=2; string api_base_url=3; string api_key=4; string default_model=5; int32 max_tokens=6; int32 request_timeout_seconds=7; }
message ConversationResponse { bool success=1; string error_message=2; ConversationMessage conversation=3; }
message ConversationMessage { string id=1; string title=2; string model=3; string system_prompt=4; string created_at=5; string updated_at=6; repeated MessageDtoMessage messages=7; }
message MessageDtoMessage { string id=1; string role=2; string content=3; string created_at=4; }
message ListConversationsResponse { bool success=1; string error_message=2; repeated ConversationSummaryMessage conversations=3; }
message ConversationSummaryMessage { string id=1; string title=2; string model=3; string created_at=4; string updated_at=5; }
message DeleteConversationResponse { bool success=1; string error_message=2; bool deleted=3; }
message ChatResponse { bool success=1; string error_message=2; string model=3; string content=4; bool done=5; int32 prompt_eval_count=6; int32 eval_count=7; }
message MessageChunk { string content=1; bool done=2; int32 eval_count=3; }
message ListModelsResponse { bool success=1; string error_message=2; repeated ModelInfoMessage models=3; }
message ModelInfoMessage { string name=1; string provider=2; }
message SettingsResponse { bool success=1; string error_message=2; string provider=3; string api_base_url=4; string default_model=5; int32 max_tokens=6; int32 request_timeout_seconds=7; }
```

#### 10.4.2 `src/Modules/AI/DotNetCloud.Modules.AI.Host/Services/AiGrpcService.cs`

```csharp
// COMPLETE FILE
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.AI.Host.Protos;
using DotNetCloud.Modules.AI.Services;
using Grpc.Core;

namespace DotNetCloud.Modules.AI.Host.Services;

public sealed class AiGrpcService : AiService.AiServiceBase
{
    private readonly IAiChatService _chatService;
    private readonly IAiSettingsProvider _settingsProvider;
    private readonly ILogger<AiGrpcService> _logger;

    public AiGrpcService(IAiChatService chatService, IAiSettingsProvider settingsProvider, ILogger<AiGrpcService> logger)
    { _chatService = chatService; _settingsProvider = settingsProvider; _logger = logger; }

    public override async Task<ConversationResponse> CreateConversation(CreateConversationRequest r, ServerCallContext ctx)
    {
        try {
            var caller = CallerContext.CreateSystemContext();
            var dm = await _settingsProvider.GetDefaultModelAsync(ctx.CancellationToken);
            var model = string.IsNullOrWhiteSpace(r.Model) ? dm : r.Model;
            var c = await _chatService.CreateConversationAsync(caller, string.IsNullOrWhiteSpace(r.Title)?null:r.Title, model, string.IsNullOrWhiteSpace(r.SystemPrompt)?null:r.SystemPrompt, ctx.CancellationToken);
            return new ConversationResponse { Success=true, Conversation=MapConversation(c) };
        } catch (Exception ex) { _logger.LogError(ex,"CreateConversation failed"); return new ConversationResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override async Task<ConversationResponse> GetConversation(GetConversationRequest r, ServerCallContext ctx)
    {
        try {
            var c = await _chatService.GetConversationAsync(CallerContext.CreateSystemContext(), Guid.Parse(r.ConversationId), ctx.CancellationToken);
            return c is null ? new ConversationResponse{Success=false,ErrorMessage="Not found"} : new ConversationResponse{Success=true,Conversation=MapConversationWithMessages(c)};
        } catch (Exception ex) { _logger.LogError(ex,"GetConversation failed"); return new ConversationResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override async Task<ListConversationsResponse> ListConversations(ListConversationsRequest r, ServerCallContext ctx)
    {
        try {
            var list = await _chatService.ListConversationsAsync(CallerContext.CreateSystemContext(), ctx.CancellationToken);
            var resp = new ListConversationsResponse{Success=true};
            foreach(var c in list) resp.Conversations.Add(MapSummary(c));
            return resp;
        } catch (Exception ex) { _logger.LogError(ex,"ListConversations failed"); return new ListConversationsResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override async Task<DeleteConversationResponse> DeleteConversation(DeleteConversationRequest r, ServerCallContext ctx)
    {
        try { var d = await _chatService.DeleteConversationAsync(CallerContext.CreateSystemContext(), Guid.Parse(r.ConversationId), ctx.CancellationToken); return new DeleteConversationResponse{Success=true,Deleted=d}; }
        catch (Exception ex) { _logger.LogError(ex,"DeleteConversation failed"); return new DeleteConversationResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override async Task<ConversationResponse> RenameConversation(RenameConversationRequest r, ServerCallContext ctx)
    {
        try { var ok = await _chatService.RenameConversationAsync(CallerContext.CreateSystemContext(), Guid.Parse(r.ConversationId), r.NewTitle, ctx.CancellationToken); return new ConversationResponse{Success=ok}; }
        catch (Exception ex) { _logger.LogError(ex,"RenameConversation failed"); return new ConversationResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override async Task<ChatResponse> SendMessage(SendMessageRequest r, ServerCallContext ctx)
    {
        try {
            var resp = await _chatService.SendMessageAsync(CallerContext.CreateSystemContext(), Guid.Parse(r.ConversationId), r.Message, ctx.CancellationToken);
            return new ChatResponse{Success=true,Model=resp.Model,Content=resp.Message.Content,Done=resp.Done,PromptEvalCount=resp.PromptEvalCount,EvalCount=resp.EvalCount};
        } catch (Exception ex) { _logger.LogError(ex,"SendMessage failed"); return new ChatResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override async Task SendMessageStream(SendMessageRequest r, IServerStreamWriter<MessageChunk> stream, ServerCallContext ctx)
    {
        await foreach(var chunk in _chatService.SendMessageStreamingAsync(CallerContext.CreateSystemContext(), Guid.Parse(r.ConversationId), r.Message, ctx.CancellationToken))
            await stream.WriteAsync(new MessageChunk{Content=chunk.Content,Done=chunk.Done,EvalCount=chunk.EvalCount});
    }

    public override async Task<ListModelsResponse> ListModels(ListModelsRequest r, ServerCallContext ctx)
    {
        try {
            var models = await _chatService.ListModelsAsync(CallerContext.CreateSystemContext(), ctx.CancellationToken);
            var resp = new ListModelsResponse{Success=true};
            foreach(var m in models) resp.Models.Add(new ModelInfoMessage{Name=m.Name,Provider=m.Provider});
            return resp;
        } catch (Exception ex) { _logger.LogError(ex,"ListModels failed"); return new ListModelsResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override async Task<SettingsResponse> GetSettings(GetSettingsRequest r, ServerCallContext ctx)
    {
        try { return new SettingsResponse{Success=true,Provider=await _settingsProvider.GetProviderAsync(ctx.CancellationToken),ApiBaseUrl=await _settingsProvider.GetApiBaseUrlAsync(ctx.CancellationToken),DefaultModel=await _settingsProvider.GetDefaultModelAsync(ctx.CancellationToken),MaxTokens=await _settingsProvider.GetMaxTokensAsync(ctx.CancellationToken),RequestTimeoutSeconds=await _settingsProvider.GetRequestTimeoutSecondsAsync(ctx.CancellationToken)}; }
        catch (Exception ex) { _logger.LogError(ex,"GetSettings failed"); return new SettingsResponse{Success=false,ErrorMessage=ex.Message}; }
    }

    public override Task<SettingsResponse> UpdateSettings(UpdateSettingsRequest r, ServerCallContext ctx)
        => Task.FromResult(new SettingsResponse{Success=true,Provider=r.Provider,ApiBaseUrl=r.ApiBaseUrl,DefaultModel=r.DefaultModel,MaxTokens=r.MaxTokens,RequestTimeoutSeconds=r.RequestTimeoutSeconds});

    static ConversationMessage MapConversation(Models.Conversation c) => new(){Id=c.Id.ToString(),Title=c.Title,Model=c.Model,SystemPrompt=c.SystemPrompt??"",CreatedAt=c.CreatedAt.ToString("O"),UpdatedAt=c.UpdatedAt.ToString("O")};
    static ConversationMessage MapConversationWithMessages(Models.Conversation c){var m=MapConversation(c);m.Messages.AddRange(c.Messages.Select(x=>new MessageDtoMessage{Id=x.Id.ToString(),Role=x.Role,Content=x.Content,CreatedAt=x.CreatedAt.ToString("O")}));return m;}
    static ConversationSummaryMessage MapSummary(Models.Conversation c) => new(){Id=c.Id.ToString(),Title=c.Title,Model=c.Model,CreatedAt=c.CreatedAt.ToString("O"),UpdatedAt=c.UpdatedAt.ToString("O")};
}
```

#### 10.4.3 `src/Modules/AI/DotNetCloud.Modules.AI/Services/IAiApiClient.cs`

```csharp
// COMPLETE FILE
namespace DotNetCloud.Modules.AI.Services;

public interface IAiApiClient
{
    Task<ConversationDto?> CreateConversationAsync(string? title, string model, string? systemPrompt, CancellationToken ct=default);
    Task<ConversationDetailDto?> GetConversationAsync(Guid conversationId, CancellationToken ct=default);
    Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(CancellationToken ct=default);
    Task<bool> DeleteConversationAsync(Guid conversationId, CancellationToken ct=default);
    Task<bool> RenameConversationAsync(Guid conversationId, string newTitle, CancellationToken ct=default);
    Task<ChatResponseDto?> SendMessageAsync(Guid conversationId, string message, CancellationToken ct=default);
    IAsyncEnumerable<MessageChunkDto> SendMessageStreamingAsync(Guid conversationId, string message, CancellationToken ct=default);
    Task<IReadOnlyList<ModelInfoDto>> ListModelsAsync(CancellationToken ct=default);
    Task<SettingsDto?> GetSettingsAsync(CancellationToken ct=default);
    Task<SettingsDto?> UpdateSettingsAsync(SettingsDto dto, CancellationToken ct=default);
}
public sealed record ConversationDto{public Guid Id{get;init;}public string Title{get;init;}="";public string Model{get;init;}="";public string? SystemPrompt{get;init;}public DateTime CreatedAt{get;init;}public DateTime UpdatedAt{get;init;}}
public sealed record ConversationDetailDto:ConversationDto{public IReadOnlyList<MessageDto> Messages{get;init;}=[];}
public sealed record ConversationSummaryDto{public Guid Id{get;init;}public string Title{get;init;}="";public string Model{get;init;}="";public DateTime CreatedAt{get;init;}public DateTime UpdatedAt{get;init;}}
public sealed record MessageDto{public Guid Id{get;init;}public string Role{get;init;}="";public string Content{get;init;}="";public DateTime CreatedAt{get;init;}}
public sealed record ChatResponseDto{public string Model{get;init;}="";public string Content{get;init;}="";public bool Done{get;init;}public int PromptEvalCount{get;init;}public int EvalCount{get;init;}}
public sealed record MessageChunkDto{public string Content{get;init;}="";public bool Done{get;init;}public int EvalCount{get;init;}}
public sealed record ModelInfoDto{public string Name{get;init;}="";public string Provider{get;init;}="";}
public sealed record SettingsDto{public string Provider{get;init;}="";public string ApiBaseUrl{get;init;}="";public string DefaultModel{get;init;}="";public int MaxTokens{get;init;}public int RequestTimeoutSeconds{get;init;}}
```

#### 10.4.4 `src/Core/DotNetCloud.Core.Server/Grpc/Clients/AiGrpcApiClient.cs`

```csharp
// COMPLETE FILE
using System.Runtime.CompilerServices;
using DotNetCloud.Modules.AI.Host.Protos;
using DotNetCloud.Modules.AI.Services;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.Grpc.Clients;

public sealed class AiGrpcClientOptions{public const string SectionName="AiGrpc";public string AiModuleAddress{get;set;}="http://localhost:5015";public TimeSpan Timeout{get;set;}=TimeSpan.FromSeconds(30);}

public sealed class AiGrpcApiClient:IAiApiClient,IDisposable
{
    private readonly AiGrpcClientOptions _opt;
    private readonly ModuleEndpointProvider _ep;
    private readonly ILogger<AiGrpcApiClient> _log;
    private readonly Lazy<GrpcChannel> _ch;
    private readonly Lazy<AiService.AiServiceClient> _cl;
    private bool _disposed;

    public AiGrpcApiClient(IOptions<AiGrpcClientOptions> o,ModuleEndpointProvider ep,ILogger<AiGrpcApiClient> log)
    {_opt=o.Value;_ep=ep;_log=log;_ch=new(CreateChannel);_cl=new(()=>new AiService.AiServiceClient(_ch.Value));}

    public async Task<ConversationDto?> CreateConversationAsync(string? title,string model,string? sp,CancellationToken ct=default)
        =>await SafeCall(async()=>{var r=new CreateConversationRequest{UserId=Guid.Empty.ToString(),Title=title??"",Model=model,SystemPrompt=sp??""};var resp=await _cl.Value.CreateConversationAsync(r,DL(ct)).ResponseAsync;return resp.Success?ToConv(resp.Conversation):null;},"CreateConversation");

    public async Task<ConversationDetailDto?> GetConversationAsync(Guid id,CancellationToken ct=default)
        =>await SafeCall(async()=>{var r=new GetConversationRequest{ConversationId=id.ToString(),UserId=Guid.Empty.ToString()};var resp=await _cl.Value.GetConversationAsync(r,DL(ct)).ResponseAsync;return resp.Success?ToConvDetail(resp.Conversation):null;},"GetConversation");

    public async Task<IReadOnlyList<ConversationSummaryDto>> ListConversationsAsync(CancellationToken ct=default)
        =>(await SafeCallList(async()=>{var r=new ListConversationsRequest{UserId=Guid.Empty.ToString()};var resp=await _cl.Value.ListConversationsAsync(r,DL(ct)).ResponseAsync;return resp.Success?resp.Conversations.Select(c=>new ConversationSummaryDto{Id=Guid.Parse(c.Id),Title=c.Title,Model=c.Model,CreatedAt=DateTime.Parse(c.CreatedAt),UpdatedAt=DateTime.Parse(c.UpdatedAt)}).ToList():(IReadOnlyList<ConversationSummaryDto>)[];},"ListConversations",Array.Empty<ConversationSummaryDto>()))!;

    public async Task<bool> DeleteConversationAsync(Guid id,CancellationToken ct=default)
        =>(await SafeCall(async()=>{var r=new DeleteConversationRequest{ConversationId=id.ToString(),UserId=Guid.Empty.ToString()};var resp=await _cl.Value.DeleteConversationAsync(r,DL(ct)).ResponseAsync;return resp.Success&&resp.Deleted;},"DeleteConversation",false))!;

    public async Task<bool> RenameConversationAsync(Guid id,string t,CancellationToken ct=default)
        =>(await SafeCall(async()=>{var r=new RenameConversationRequest{ConversationId=id.ToString(),UserId=Guid.Empty.ToString(),NewTitle=t};var resp=await _cl.Value.RenameConversationAsync(r,DL(ct)).ResponseAsync;return resp.Success;},"RenameConversation",false))!;

    public async Task<ChatResponseDto?> SendMessageAsync(Guid id,string msg,CancellationToken ct=default)
        =>await SafeCall(async()=>{var r=new SendMessageRequest{ConversationId=id.ToString(),UserId=Guid.Empty.ToString(),Message=msg};var resp=await _cl.Value.SendMessageAsync(r,DL(ct)).ResponseAsync;return resp.Success?new ChatResponseDto{Model=resp.Model,Content=resp.Content,Done=resp.Done,PromptEvalCount=resp.PromptEvalCount,EvalCount=resp.EvalCount}:null;},"SendMessage");

    public async IAsyncEnumerable<MessageChunkDto> SendMessageStreamingAsync(Guid id,string msg,[EnumeratorCancellation]CancellationToken ct=default)
    {
        var r=new SendMessageRequest{ConversationId=id.ToString(),UserId=Guid.Empty.ToString(),Message=msg};
        using var call=_cl.Value.SendMessageStream(r,DL(ct));
        await foreach(var c in call.ResponseStream.ReadAllAsync(ct))
            yield return new MessageChunkDto{Content=c.Content,Done=c.Done,EvalCount=c.EvalCount};
    }

    public async Task<IReadOnlyList<ModelInfoDto>> ListModelsAsync(CancellationToken ct=default)
        =>(await SafeCallList(async()=>{var r=new ListModelsRequest{UserId=Guid.Empty.ToString()};var resp=await _cl.Value.ListModelsAsync(r,DL(ct)).ResponseAsync;return resp.Success?resp.Models.Select(m=>new ModelInfoDto{Name=m.Name,Provider=m.Provider}).ToList():(IReadOnlyList<ModelInfoDto>)[];},"ListModels",Array.Empty<ModelInfoDto>()))!;

    public async Task<SettingsDto?> GetSettingsAsync(CancellationToken ct=default)
        =>await SafeCall(async()=>{var r=new GetSettingsRequest{UserId=Guid.Empty.ToString()};var resp=await _cl.Value.GetSettingsAsync(r,DL(ct)).ResponseAsync;return resp.Success?new SettingsDto{Provider=resp.Provider,ApiBaseUrl=resp.ApiBaseUrl,DefaultModel=resp.DefaultModel,MaxTokens=resp.MaxTokens,RequestTimeoutSeconds=resp.RequestTimeoutSeconds}:null;},"GetSettings");

    public async Task<SettingsDto?> UpdateSettingsAsync(SettingsDto d,CancellationToken ct=default)
        =>await SafeCall(async()=>{var r=new UpdateSettingsRequest{UserId=Guid.Empty.ToString(),Provider=d.Provider,ApiBaseUrl=d.ApiBaseUrl,DefaultModel=d.DefaultModel,MaxTokens=d.MaxTokens,RequestTimeoutSeconds=d.RequestTimeoutSeconds};var resp=await _cl.Value.UpdateSettingsAsync(r,DL(ct)).ResponseAsync;return resp.Success?new SettingsDto{Provider=resp.Provider,ApiBaseUrl=resp.ApiBaseUrl,DefaultModel=resp.DefaultModel,MaxTokens=resp.MaxTokens,RequestTimeoutSeconds=resp.RequestTimeoutSeconds}:null;},"UpdateSettings");

    static ConversationDto ToConv(ConversationMessage c)=>new(){Id=Guid.Parse(c.Id),Title=c.Title,Model=c.Model,SystemPrompt=string.IsNullOrEmpty(c.SystemPrompt)?null:c.SystemPrompt,CreatedAt=DateTime.Parse(c.CreatedAt),UpdatedAt=DateTime.Parse(c.UpdatedAt)};
    static ConversationDetailDto ToConvDetail(ConversationMessage c)=>new(){Id=Guid.Parse(c.Id),Title=c.Title,Model=c.Model,SystemPrompt=string.IsNullOrEmpty(c.SystemPrompt)?null:c.SystemPrompt,CreatedAt=DateTime.Parse(c.CreatedAt),UpdatedAt=DateTime.Parse(c.UpdatedAt),Messages=c.Messages.Select(m=>new MessageDto{Id=Guid.Parse(m.Id),Role=m.Role,Content=m.Content,CreatedAt=DateTime.Parse(m.CreatedAt)}).ToList()};

    async Task<T> SafeCallList<T>(Func<Task<T>> c,string o,T fb)where T:class{try{return await c();}catch(RpcException ex)when(ex.StatusCode==StatusCode.Unavailable){_log.LogWarning("AI {Op} unavailable",o);}catch(Exception ex){_log.LogError(ex,"AI {Op} error",o);}return fb;}
    async Task<T?> SafeCall<T>(Func<Task<T?>> c,string o,T? fb=default){try{return await c();}catch(RpcException ex)when(ex.StatusCode==StatusCode.Unavailable){_log.LogWarning("AI {Op} unavailable",o);}catch(Exception ex){_log.LogError(ex,"AI {Op} error",o);}return fb;}
    CallOptions DL(CancellationToken ct)=>new(deadline:DateTime.UtcNow.Add(_opt.Timeout),cancellationToken:ct);
    GrpcChannel CreateChannel(){var a=_ep.GetEndpoint("dotnetcloud.ai");_log.LogInformation("AiGrpcApiClient connecting to {A}",a);return GrpcChannel.ForAddress(a,new GrpcChannelOptions{HttpHandler=new SocketsHttpHandler{EnableMultipleHttp2Connections=true,ConnectTimeout=TimeSpan.FromSeconds(5)}});}
    public void Dispose(){if(!_disposed){_disposed=true;if(_ch.IsValueCreated)try{_ch.Value.Dispose();}catch{}}}
}
```

#### 10.4.5 Updated `src/Modules/AI/DotNetCloud.Modules.AI.Host/Program.cs` — REPLACE ENTIRE FILE

```csharp
// COMPLETE FILE
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.AI;
using DotNetCloud.Modules.AI.Data;
using DotNetCloud.Modules.AI.Host.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if(!string.IsNullOrEmpty(configDir)){var p=Path.Combine(configDir,"config.json");if(File.Exists(p))builder.Configuration.AddJsonFile(p,optional:true,reloadOnChange:false);}
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if(!string.IsNullOrEmpty(grpcEndpoint)){var uri=new Uri(grpcEndpoint.Replace("unix://","http://").Replace("net.pipe://","http://"));builder.WebHost.ConfigureKestrel(o=>o.Listen(System.Net.IPAddress.Loopback,uri.Port,l=>l.Protocols=HttpProtocols.Http2));}
builder.Services.AddSingleton<AiModule>();
var cs=builder.Configuration["connectionString"]??builder.Configuration.GetConnectionString("DefaultConnection");
var dp=builder.Configuration["databaseProvider"]??builder.Configuration["database:provider"];
if(!string.IsNullOrEmpty(cs)&&!string.IsNullOrEmpty(dp)){builder.Services.AddDbContext<AiDbContext>(o=>{if(string.Equals(dp,"PostgreSql",StringComparison.OrdinalIgnoreCase))o.UseNpgsql(cs);else o.UseSqlServer(cs);});}
else{builder.Services.AddDbContext<AiDbContext>(o=>o.UseInMemoryDatabase("AiModule"));}
builder.Services.AddSingleton<IEventBus,InProcessEventBus>();
builder.Services.AddAiServices(builder.Configuration);
builder.Services.AddGrpc();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddCheck<AiHealthCheck>("ai_module");
var app=builder.Build();
app.MapGrpcService<AiGrpcService>();
app.MapGrpcService<AiLifecycleService>();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/",()=>Results.Ok(new{module="dotnetcloud.ai",version="1.0.0",status="running"}));
await app.RunAsync();
```

#### 10.4.6 Updated `src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj` — REPLACE ENTIRE FILE

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>DotNetCloud.Modules.AI.Host</RootNamespace><AssemblyName>dotnetcloud.ai</AssemblyName>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PublishWithPackageReferences>false</PublishWithPackageReferences><PublishReadyToCompile>false</PublishReadyToCompile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Grpc.AspNetCore" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design"><PrivateAssets>all</PrivateAssets><IncludeAssets>runtime;build;native;contentfiles;analyzers;buildtransitive</IncludeAssets></PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="OpenIddict.Validation.AspNetCore" />
  </ItemGroup>
  <ItemGroup><Protobuf Include="Protos\ai_service.proto" GrpcServices="Server" /></ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DotNetCloud.Modules.AI\DotNetCloud.Modules.AI.csproj" />
    <ProjectReference Include="..\DotNetCloud.Modules.AI.Data\DotNetCloud.Modules.AI.Data.csproj" />
    <ProjectReference Include="..\..\..\Core\DotNetCloud.Core.Grpc\DotNetCloud.Core.Grpc.csproj" />
  </ItemGroup>
</Project>
```

### 10.5 Phase 3 Module Host `Program.cs` Hardening

For Music, Photos, Video, Search — add these blocks. Add at the TOP (after `var builder = ...`):

```csharp
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if(!string.IsNullOrEmpty(configDir)){var p=Path.Combine(configDir,"config.json");if(File.Exists(p))builder.Configuration.AddJsonFile(p,optional:true,reloadOnChange:false);}
var grpcEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_GRPC_ENDPOINT");
if(!string.IsNullOrEmpty(grpcEndpoint)){var uri=new Uri(grpcEndpoint.Replace("unix://","http://").Replace("net.pipe://","http://"));builder.WebHost.ConfigureKestrel(o=>o.Listen(System.Net.IPAddress.Loopback,uri.Port,l=>l.Protocols=Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2));}
```

Replace hardcoded `UseInMemoryDatabase` with:

```csharp
var cs=builder.Configuration["connectionString"]??builder.Configuration.GetConnectionString("DefaultConnection");
var dp=builder.Configuration["databaseProvider"]??builder.Configuration["database:provider"];
if(!string.IsNullOrEmpty(cs)&&!string.IsNullOrEmpty(dp)){builder.Services.AddDbContext<XxxDbContext>(o=>{if(string.Equals(dp,"PostgreSql",StringComparison.OrdinalIgnoreCase))o.UseNpgsql(cs);else o.UseSqlServer(cs);});}
else{builder.Services.AddDbContext<XxxDbContext>(o=>o.UseInMemoryDatabase("XxxModule"));}
```

Music additionally needs `DbContextFactory` and `ITableNamingStrategy` — retain the existing pattern but make it config-driven (see Contacts Program.cs pattern).

Add after existing domain gRPC mapping: `app.MapGrpcService<XxxLifecycleService>();`

### 10.6 Manifest Files

All 10 missing manifests. Contents in single-line JSON:

| File                                  | Content                                                                                                                                                                                                                                                                                                                                                                  |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `src/Modules/Chat/manifest.json`      | `{"id":"dotnetcloud.chat","name":"Chat","version":"1.0.0","description":"Real-time messaging.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger"],"publishedEvents":["MessageSentEvent","ChannelCreatedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                                |
| `src/Modules/Files/manifest.json`     | `{"id":"dotnetcloud.files","name":"Files","version":"1.0.0","description":"File storage, sharing, versioning.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger","ISearchProvider"],"publishedEvents":["FileUploadedEvent","FileSharedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}` |
| `src/Modules/Music/manifest.json`     | `{"id":"dotnetcloud.music","name":"Music","version":"1.0.0","description":"Music library.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger"],"publishedEvents":["TrackAddedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                                                           |
| `src/Modules/Photos/manifest.json`    | `{"id":"dotnetcloud.photos","name":"Photos","version":"1.0.0","description":"Photo management.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger"],"publishedEvents":["PhotoUploadedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                                                   |
| `src/Modules/Video/manifest.json`     | `{"id":"dotnetcloud.video","name":"Video","version":"1.0.0","description":"Video management.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger"],"publishedEvents":["VideoUploadedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                                                     |
| `src/Modules/Search/manifest.json`    | `{"id":"dotnetcloud.search","name":"Search","version":"1.0.0","description":"Full-text search.","author":"DotNetCloud","requiredCapabilities":["IUserDirectory","ICurrentUserContext","IAuditLogger"],"publishedEvents":["DocumentIndexedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                                                                        |
| `src/Modules/Tracks/manifest.json`    | `{"id":"dotnetcloud.tracks","name":"Tracks","version":"1.0.0","description":"Project management.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger","IOrganizationDirectory"],"publishedEvents":["WorkItemCreatedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                      |
| `src/Modules/Bookmarks/manifest.json` | `{"id":"dotnetcloud.bookmarks","name":"Bookmarks","version":"1.0.0","description":"Bookmark management.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger"],"publishedEvents":["BookmarkCreatedEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                                        |
| `src/Modules/Email/manifest.json`     | `{"id":"dotnetcloud.email","name":"Email","version":"1.0.0","description":"Email client.","author":"DotNetCloud","requiredCapabilities":["INotificationService","IUserDirectory","ICurrentUserContext","IAuditLogger"],"publishedEvents":["EmailReceivedEvent","EmailSentEvent"],"subscribedEvents":[],"minCoreVersion":"1.0.0"}`                                        |

### 10.7 Core.Server Exact Edits

#### 10.7.1 Lines to REMOVE from `csproj`

```xml
    <ProjectReference Include="..\..\Modules\Chat\DotNetCloud.Modules.Chat.Host\DotNetCloud.Modules.Chat.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Files\DotNetCloud.Modules.Files.Host\DotNetCloud.Modules.Files.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Notes\DotNetCloud.Modules.Notes.Host\DotNetCloud.Modules.Notes.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Tracks\DotNetCloud.Modules.Tracks.Host\DotNetCloud.Modules.Tracks.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Photos\DotNetCloud.Modules.Photos.Host\DotNetCloud.Modules.Photos.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Music\DotNetCloud.Modules.Music.Host\DotNetCloud.Modules.Music.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Video\DotNetCloud.Modules.Video.Host\DotNetCloud.Modules.Video.Host.csproj" />
    <ProjectReference Include="..\..\Modules\About\DotNetCloud.Modules.About.Host\DotNetCloud.Modules.About.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Search\DotNetCloud.Modules.Search.Host\DotNetCloud.Modules.Search.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Bookmarks\DotNetCloud.Modules.Bookmarks.Host\DotNetCloud.Modules.Bookmarks.Host.csproj" />
    <ProjectReference Include="..\..\Modules\Email\DotNetCloud.Modules.Email.Host\DotNetCloud.Modules.Email.Host.csproj" />
```

Also remove AI.Host if present (check current csproj).

#### 10.7.2 Lines to ADD to `csproj` (proto ItemGroup)

```xml
    <Protobuf Include="..\..\Modules\Chat\DotNetCloud.Modules.Chat.Host\Protos\chat_service.proto" GrpcServices="Client" Link="Protos\chat_service.proto" />
    <Protobuf Include="..\..\Modules\Files\DotNetCloud.Modules.Files.Host\Protos\files_service.proto" GrpcServices="Client" Link="Protos\files_service.proto" />
    <Protobuf Include="..\..\Modules\Notes\DotNetCloud.Modules.Notes.Host\Protos\notes_service.proto" GrpcServices="Client" Link="Protos\notes_service.proto" />
    <Protobuf Include="..\..\Modules\Tracks\DotNetCloud.Modules.Tracks.Host\Protos\tracks_service.proto" GrpcServices="Client" Link="Protos\tracks_service.proto" />
    <Protobuf Include="..\..\Modules\Music\DotNetCloud.Modules.Music.Host\Protos\music_service.proto" GrpcServices="Client" Link="Protos\music_service.proto" />
    <Protobuf Include="..\..\Modules\Photos\DotNetCloud.Modules.Photos.Host\Protos\photos_service.proto" GrpcServices="Client" Link="Protos\photos_service.proto" />
    <Protobuf Include="..\..\Modules\Video\DotNetCloud.Modules.Video.Host\Protos\video_service.proto" GrpcServices="Client" Link="Protos\video_service.proto" />
    <Protobuf Include="..\..\Modules\Search\DotNetCloud.Modules.Search.Host\Protos\search_service.proto" GrpcServices="Client" Link="Protos\search_service.proto" />
    <Protobuf Include="..\..\Modules\Bookmarks\DotNetCloud.Modules.Bookmarks.Host\Protos\bookmarks_service.proto" GrpcServices="Client" Link="Protos\bookmarks_service.proto" />
    <Protobuf Include="..\..\Modules\Email\DotNetCloud.Modules.Email.Host\Protos\email_service.proto" GrpcServices="Client" Link="Protos\email_service.proto" />
    <Protobuf Include="..\..\Modules\About\DotNetCloud.Modules.About.Host\Protos\about_service.proto" GrpcServices="Client" Link="Protos\about_service.proto" />
    <Protobuf Include="..\..\Modules\AI\DotNetCloud.Modules.AI.Host\Protos\ai_service.proto" GrpcServices="Client" Link="Protos\ai_service.proto" />
```

#### 10.7.3 Lines to REMOVE from `Program.cs`

```csharp
        builder.Services.AddFilesServices(builder.Configuration);
        builder.Services.AddChatServices(builder.Configuration);
        builder.Services.AddNotesServices(builder.Configuration);
        builder.Services.AddTracksServices(builder.Configuration);
        builder.Services.AddPhotosServices(builder.Configuration);
        builder.Services.AddMusicServices(builder.Configuration);
        builder.Services.AddVideoServices(builder.Configuration);
        builder.Services.AddAiServices(builder.Configuration);
        builder.Services.AddSearchServices(builder.Configuration);
        builder.Services.AddBookmarksServices(builder.Configuration);
        builder.Services.AddEmailServices(builder.Configuration);
```

#### 10.7.4 In-process client registrations to CHANGE in `Program.cs`

```csharp
// CHANGE:
builder.Services.AddScoped<DotNetCloud.Modules.Notes.Services.INotesApiClient, DotNetCloud.Modules.Notes.Services.NotesApiClient>();
// TO:
builder.Services.AddScoped<DotNetCloud.Modules.Notes.Services.INotesApiClient, DotNetCloud.Core.Server.Grpc.Clients.NotesGrpcApiClient>();

// CHANGE:
builder.Services.AddScoped<DotNetCloud.Modules.Tracks.Services.ITracksApiClient, DotNetCloud.Modules.Tracks.Services.TracksApiClient>();
// TO:
builder.Services.AddScoped<DotNetCloud.Modules.Tracks.Services.ITracksApiClient, DotNetCloud.Core.Server.Grpc.Clients.TracksGrpcApiClient>();

// CHANGE:
builder.Services.AddScoped<DotNetCloud.Modules.Email.Services.IEmailApiClient, DotNetCloud.Modules.Email.Services.EmailApiClient>();
// TO:
builder.Services.AddScoped<DotNetCloud.Modules.Email.Services.IEmailApiClient, DotNetCloud.Core.Server.Grpc.Clients.EmailGrpcApiClient>();

// CHANGE:
builder.Services.AddScoped<DotNetCloud.Modules.Bookmarks.Services.IBookmarksApiClient, DotNetCloud.Modules.Bookmarks.Services.BookmarksApiClient>();
// TO:
builder.Services.AddScoped<DotNetCloud.Modules.Bookmarks.Services.IBookmarksApiClient, DotNetCloud.Core.Server.Grpc.Clients.BookmarksGrpcApiClient>();
```

#### 10.7.5 Options bindings to ADD to `Program.cs` (after existing Contacts/Calendar block)

```csharp
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.ChatGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.ChatGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.FilesGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.FilesGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.NotesGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.NotesGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.TracksGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.TracksGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.MusicGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.MusicGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.PhotosGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.PhotosGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.VideoGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.VideoGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.SearchGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.SearchGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.BookmarksGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.BookmarksGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.EmailGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.EmailGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.AboutGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.AboutGrpcClientOptions.SectionName));
        builder.Services.Configure<DotNetCloud.Core.Server.Grpc.Clients.AiGrpcClientOptions>(builder.Configuration.GetSection(DotNetCloud.Core.Server.Grpc.Clients.AiGrpcClientOptions.SectionName));
```

#### 10.7.6 New client registrations to ADD to `Program.cs` (after `ModuleEndpointProvider`)

```csharp
        builder.Services.AddScoped<DotNetCloud.Modules.Chat.Services.IChatApiClient, DotNetCloud.Core.Server.Grpc.Clients.ChatGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Files.Services.IFilesApiClient, DotNetCloud.Core.Server.Grpc.Clients.FilesGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Music.Services.IMusicApiClient, DotNetCloud.Core.Server.Grpc.Clients.MusicGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Photos.Services.IPhotosApiClient, DotNetCloud.Core.Server.Grpc.Clients.PhotosGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Video.Services.IVideoApiClient, DotNetCloud.Core.Server.Grpc.Clients.VideoGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.Search.Services.ISearchApiClient, DotNetCloud.Core.Server.Grpc.Clients.SearchGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.About.Services.IAboutApiClient, DotNetCloud.Core.Server.Grpc.Clients.AboutGrpcApiClient>();
        builder.Services.AddScoped<DotNetCloud.Modules.AI.Services.IAiApiClient, DotNetCloud.Core.Server.Grpc.Clients.AiGrpcApiClient>();
```

### 10.8 Deployment Scripts

#### `scripts/publish-module-hosts.ps1` — REPLACE `$modules` array

```powershell
$modules = @(
    @{ Id = "dotnetcloud.contacts";  Project = "src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/DotNetCloud.Modules.Contacts.Host.csproj" },
    @{ Id = "dotnetcloud.calendar";  Project = "src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj" },
    @{ Id = "dotnetcloud.about";     Project = "src/Modules/About/DotNetCloud.Modules.About.Host/DotNetCloud.Modules.About.Host.csproj" },
    @{ Id = "dotnetcloud.ai";        Project = "src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj" },
    @{ Id = "dotnetcloud.music";     Project = "src/Modules/Music/DotNetCloud.Modules.Music.Host/DotNetCloud.Modules.Music.Host.csproj" },
    @{ Id = "dotnetcloud.photos";    Project = "src/Modules/Photos/DotNetCloud.Modules.Photos.Host/DotNetCloud.Modules.Photos.Host.csproj" },
    @{ Id = "dotnetcloud.video";     Project = "src/Modules/Video/DotNetCloud.Modules.Video.Host/DotNetCloud.Modules.Video.Host.csproj" },
    @{ Id = "dotnetcloud.search";    Project = "src/Modules/Search/DotNetCloud.Modules.Search.Host/DotNetCloud.Modules.Search.Host.csproj" },
    @{ Id = "dotnetcloud.chat";      Project = "src/Modules/Chat/DotNetCloud.Modules.Chat.Host/DotNetCloud.Modules.Chat.Host.csproj" },
    @{ Id = "dotnetcloud.files";     Project = "src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj" },
    @{ Id = "dotnetcloud.notes";     Project = "src/Modules/Notes/DotNetCloud.Modules.Notes.Host/DotNetCloud.Modules.Notes.Host.csproj" },
    @{ Id = "dotnetcloud.tracks";    Project = "src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/DotNetCloud.Modules.Tracks.Host.csproj" },
    @{ Id = "dotnetcloud.bookmarks"; Project = "src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/DotNetCloud.Modules.Bookmarks.Host.csproj" },
    @{ Id = "dotnetcloud.email";     Project = "src/Modules/Email/DotNetCloud.Modules.Email.Host/DotNetCloud.Modules.Email.Host.csproj" }
)
```

#### `scripts/deploy.sh` — REPLACE the `for module in Contacts Calendar` loop

```bash
for module in Contacts Calendar About AI Music Photos Video Search Chat Files Notes Tracks Bookmarks Email; do
    module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
    dotnet publish "$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/DotNetCloud.Modules.$module.Host.csproj" \
        -c "$CONFIG" -o "$MODULES_DIR/dotnetcloud.$module_lower" --no-self-contained \
        /p:DebugType=None /p:DebugSymbols=false
done
```

### 10.9 Implementation Order (For Lesser LLM)

1. **Phase 1:** Documentation (4 files, pure text insertion)
2. **Phase 2.1:** About — create 5 new files (proto, GrpcService, IAboutApiClient, AboutGrpcApiClient, manifest.json) + replace 3 files (Program.cs, csproj, Core.Server csproj edits)
3. **Phase 2.2:** AI — create 4 new files (proto, GrpcService, LifecycleService, IAiApiClient, AiGrpcApiClient) + replace 3 files (Program.cs, csproj, Core.Server edits)
4. **Phase 3:** Music first, then Photos, Video, Search — for each: create LifecycleService (from 10.1 template) + IXxxApiClient + manifest.json + XxxGrpcApiClient + update Program.cs + update csproj + Core.Server edits
5. **Phase 4:** Chat, Files, Notes, Tracks, Bookmarks, Email — for each: create IXxxApiClient (if needed) + XxxGrpcApiClient + manifest.json + update Program.cs (env vars) + Core.Server edits
6. **Phase 5:** Replace publish-module-hosts.ps1 $modules array + replace deploy.sh loop
7. **Phase 6-7:** Cross-cutting cleanup + `dotnet build` + `dotnet test` verification

**Each module is independently buildable.** Build and test after each module conversion to catch errors early.

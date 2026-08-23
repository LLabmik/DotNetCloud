# Tracks Module — Finish Implementation Plan

> **Created:** 2026-08-02
> **Branch:** `feature/finish-tracks-module`
> **Scope:** gRPC process isolation fix, sprint planning chat, documentation cleanup, unit/integration tests
> **Prerequisite:** Build must succeed after each phase before proceeding to next

---

## Audit Findings Summary

A comprehensive document-vs-code audit (2026-08-02) identified the following issues:

| #   | Severity | Finding                                                                                                                                                                 |
| --- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | 🔴 Bug   | `Program.cs` registers `ITracksApiClient` twice — gRPC (line ~538) AND HTTP (line ~586). HTTP wins, so Tracks is NOT using gRPC despite full infrastructure being ready |
| 2   | 🔴 Gap   | Sprint Planning, Review Sessions, and Poker have zero chat/discussion capability                                                                                        |
| 3   | 🟡 Drift | `TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md` lists 16+ features as MISSING that code has                                                                                  |
| 4   | 🟡 Drift | `TRACKS_HIERARCHY_EXPANSION.md` header says "UI + tests pending" — both exist                                                                                           |
| 5   | 🟡 Drift | `MASTER_PROJECT_PLAN.md` Phase 4.1 references `BoardDto`/`CardDto` — code uses `ProductDto`/`WorkItemDto`                                                               |
| 6   | 🟢 Note  | `TracksDbInitializer` only runs migrations — by design (no seed data needed for user-created content)                                                                   |

---

## Files Inventory

### Files Modified

| File                                                                                      | Phase | Change                                                              |
| ----------------------------------------------------------------------------------------- | ----- | ------------------------------------------------------------------- |
| `src/Core/DotNetCloud.Core.Server/Program.cs`                                             | 1     | Remove HTTP `ITracksApiClient` registration + `AddTracksUiServices` |
| `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`                         | 1     | Remove `.Tracks` and `.Tracks.Data` project refs                    |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/TracksDbContext.cs`                   | 2     | Add `DbSet<SprintDiscussion>`                                       |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksRealtimeService.cs`        | 2     | Add 2 discussion broadcast methods                                  |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksRealtimeService.cs`         | 2     | Implement 2 discussion broadcast methods                            |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksSignalRService.cs`         | 2     | Add 2 discussion SignalR events                                     |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksInProcessSignalRService.cs` | 2     | Implement 2 discussion SignalR events                               |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksApiClient.cs`              | 2     | Add 4 discussion API methods                                        |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksApiClient.cs`               | 2     | Implement 4 discussion API methods (HTTP)                           |
| `src/Core/DotNetCloud.Core.Server/Grpc/Clients/TracksGrpcApiClient.cs`                    | 2     | Implement 4 discussion API methods (gRPC)                           |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Protos/tracks_service.proto`          | 2     | Add 4 discussion RPCs + message types                               |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Services/TracksGrpcService.cs`        | 2     | Implement 4 discussion RPCs                                         |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/SprintPlanningView.razor`               | 2     | Add discussion panel (right sidebar)                                |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/SprintPlanningView.razor.cs`            | 2     | Add discussion logic                                                |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionHost.razor`                | 2     | Add discussion panel                                                |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionHost.razor.cs`             | 2     | Add discussion logic                                                |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionParticipant.razor`         | 2     | Add discussion panel                                                |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionParticipant.razor.cs`      | 2     | Add discussion logic                                                |
| `docs/TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md`                                           | 4     | Rewrite feature matrix                                              |
| `docs/TRACKS_HIERARCHY_EXPANSION.md`                                                      | 4     | Update status header                                                |
| `docs/IMPLEMENTATION_CHECKLIST.md`                                                        | 4     | Add/update Tracks entries                                           |
| `docs/MASTER_PROJECT_PLAN.md`                                                             | 4     | Update Phase 4 entries                                              |

### New Files Created

| File                                                                                                | Phase | Purpose           |
| --------------------------------------------------------------------------------------------------- | ----- | ----------------- |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/SprintDiscussion.cs`                          | 2     | Entity            |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/SprintDiscussionConfiguration.cs` | 2     | EF config         |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/SprintDiscussionService.cs`            | 2     | Business logic    |
| `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/SprintDiscussionsController.cs`     | 2     | REST API          |
| `tests/DotNetCloud.Modules.Tracks.Tests/SprintDiscussionServiceTests.cs`                            | 3     | Unit tests        |
| `tests/DotNetCloud.Modules.Tracks.Tests/ReviewSessionChatTests.cs`                                  | 3     | Integration tests |
| `tests/DotNetCloud.Modules.Tracks.Tests/TracksGrpcApiClientTests.cs`                                | 3     | gRPC client tests |

---

## Phase 1: gRPC Process Isolation Fix

**Goal:** Fix the dual-registration bug so Tracks communicates exclusively via gRPC with process isolation.

> ⚠️ **Risk:** Before removing `AddTracksUiServices`, verify how Tracks Blazor components are served. If Core.Server renders them server-side (not via the Tracks host), removing this will break the UI. If they're served via the Tracks Host process, removal is safe. If Core.Server renders them, keep the registration but strip it to DbContext-only (remove business service registrations).

### Step 1.1: Remove dual HTTP registration from Program.cs

**File:** `src/Core/DotNetCloud.Core.Server/Program.cs`

Find and delete this line (around line 586):

```csharp
builder.Services.AddHttpClient<DotNetCloud.Modules.Tracks.Services.ITracksApiClient, DotNetCloud.Modules.Tracks.Services.TracksApiClient>(client =>
{
    // ... configuration ...
});
```

The gRPC registration (around line 538) is already correct:

```csharp
builder.Services.AddScoped<DotNetCloud.Core.Services.ModuleApis.ITracksApiClient, DotNetCloud.Core.Server.Grpc.Clients.TracksGrpcApiClient>();
```

**Verification:**

```bash
rg "AddHttpClient.*ITracksApiClient" src/Core/DotNetCloud.Core.Server/Program.cs
# Should return nothing
```

---

### Step 1.2: Remove AddTracksUiServices from Program.cs

**File:** `src/Core/DotNetCloud.Core.Server/Program.cs`

Find and delete this line (around line 299):

```csharp
builder.Services.AddTracksUiServices(builder.Configuration!, provider, connectionString);
```

> 🔴 **CRITICAL CHECK FIRST:** Grep the project for `AddTracksUiServices` to find all callers and verify none are needed in Core.Server. Run: `rg "AddTracksUiServices" src/` to see all usages. If the only usage is in `Core.Server/Program.cs`, it's safe to remove.

---

### Step 1.3: Remove in-process Tracks project refs from Core.Server.csproj

**File:** `src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj`

Remove these two lines from the `<ItemGroup>` that contains ProjectReferences:

```xml
<ProjectReference Include="..\..\Modules\Tracks\DotNetCloud.Modules.Tracks\DotNetCloud.Modules.Tracks.csproj" />
<ProjectReference Include="..\..\Modules\Tracks\DotNetCloud.Modules.Tracks.Data\DotNetCloud.Modules.Tracks.Data.csproj" />
```

**KEEP** these (needed for migrations and gRPC client):

```xml
<ProjectReference Include="..\..\Modules\Tracks\DotNetCloud.Modules.Tracks.Data.SqlServer\DotNetCloud.Modules.Tracks.Data.SqlServer.csproj" />
```

```xml
<Protobuf Include="..\..\Modules\Tracks\DotNetCloud.Modules.Tracks.Host\Protos\tracks_service.proto"
          GrpcServices="Client"
          Link="Protos\tracks_service.proto" />
```

---

### Step 1.4: Fix compilation errors from removed references

After removing the project references, any code in Core.Server that directly imports types from `DotNetCloud.Modules.Tracks` or `DotNetCloud.Modules.Tracks.Data` will fail to compile. Common fixes:

1. **Namespace imports** — Remove `using DotNetCloud.Modules.Tracks.*` from files that don't need them
2. **DTO references** — If Core.Server code references Tracks DTOs, those DTOs need to be moved to a shared location OR the code needs to use gRPC-generated types instead
3. **Direct service injection** — Any `ISprintService`, `IProductService` etc. injected in Core.Server must use the gRPC client (`ITracksApiClient`) instead

Run `dotnet build DotNetCloud.CI.slnf 2>&1 | head -50` and fix each error systematically.

---

### Step 1.5: Verify build

```bash
dotnet build DotNetCloud.CI.slnf -c Release
```

Must succeed with 0 errors and 0 warnings.

---

## Phase 2: Sprint Planning Chat & Discussion

**Goal:** Add real-time chat/discussion to Sprint Planning, Review Session Host, and Review Session Participant views.

### Step 2.1: Create SprintDiscussion entity

**New file:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/SprintDiscussion.cs`

```csharp
namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A chat message posted during sprint planning or review sessions.
/// Scoped to a sprint or a review session — exactly one FK must be set.
/// </summary>
public sealed class SprintDiscussion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Sprint this message belongs to. Null if review-session-scoped.</summary>
    public Guid? SprintId { get; set; }

    /// <summary>Review session this message belongs to. Null if sprint-scoped.</summary>
    public Guid? ReviewSessionId { get; set; }

    /// <summary>User who sent the message. Cross-module ref, no DB FK.</summary>
    public Guid UserId { get; set; }

    /// <summary>Display name snapshot at post time (avoids cross-module lookup).</summary>
    public required string UserDisplayName { get; set; }

    /// <summary>Message content (plain text, max 2000 chars).</summary>
    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Sprint? Sprint { get; set; }
    public ReviewSession? ReviewSession { get; set; }
}
```

**App-level validation constraint:** Exactly one of `SprintId` or `ReviewSessionId` must be non-null. Enforce in `SprintDiscussionService`, not at the DB level.

---

### Step 2.2: Create EF configuration

**New file:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/SprintDiscussionConfiguration.cs`

Follow the pattern from `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Configuration/ActivityConfiguration.cs`:

```csharp
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DotNetCloud.Modules.Tracks.Data.Configuration;

public sealed class SprintDiscussionConfiguration : IEntityTypeConfiguration<SprintDiscussion>
{
    public void Configure(EntityTypeBuilder<SprintDiscussion> builder)
    {
        var naming = new PostgreSqlNamingStrategy(); // placeholder — resolved at runtime

        builder.ToTable(naming.GetTableName("SprintDiscussions"), naming.GetSchemaForModule("tracks"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Content).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.UserDisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CreatedAt).IsRequired();

        // Indexes for chronological fetch per scope
        builder.HasIndex(x => new { x.SprintId, x.CreatedAt });
        builder.HasIndex(x => new { x.ReviewSessionId, x.CreatedAt });

        // FKs with cascade delete
        builder.HasOne(x => x.Sprint)
            .WithMany()
            .HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewSession)
            .WithMany()
            .HasForeignKey(x => x.ReviewSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // No FK on UserId — cross-module reference
    }
}
```

> **Note:** The configuration above uses `PostgreSqlNamingStrategy` as a placeholder. Look at how other configurations in the same directory handle the naming strategy (some use constructor injection of `ITableNamingStrategy`). Follow the prevailing pattern in that directory exactly — if others inject it, do the same.

---

### Step 2.3: Add DbSet to TracksDbContext

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/TracksDbContext.cs`

Add the DbSet property alongside existing ones (alphabetically near `Sprints`):

```csharp
public DbSet<SprintDiscussion> SprintDiscussions => Set<SprintDiscussion>();
```

In `OnModelCreating`, apply the configuration:

```csharp
modelBuilder.ApplyConfiguration(new SprintDiscussionConfiguration());
```

---

### Step 2.4: Create DTOs

Add to an existing DTOs file or create a new `SprintDiscussionDtos.cs` in the Models folder or wherever other Tracks DTOs live. Find the pattern by checking:

```bash
rg "record.*Dto" src/Modules/Tracks/DotNetCloud.Modules.Tracks/Models/ | head -5
```

Create:

```csharp
namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>DTO for a sprint/review discussion message.</summary>
public sealed record SprintDiscussionDto(
    Guid Id,
    Guid? SprintId,
    Guid? ReviewSessionId,
    Guid UserId,
    string UserDisplayName,
    string Content,
    DateTime CreatedAt
);

/// <summary>Request DTO for sending a discussion message.</summary>
public sealed record SendSprintDiscussionDto(string Content);
```

---

### Step 2.5: Create database migrations

Create BOTH migrations. Run from the repo root:

```bash
# PostgreSQL migration
dotnet ef migrations add AddSprintDiscussion \
  --project src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data \
  --context TracksDbContext

# SQL Server migration
dotnet ef migrations add AddSprintDiscussion_SqlServer \
  --project src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data.SqlServer \
  --context 'DotNetCloud.Modules.Tracks.Data.TracksDbContext' \
  --output-dir Migrations
```

Verify migrations were created:

```bash
find src/Modules/Tracks -name "*AddSprintDiscussion*" -type f
```

---

### Step 2.6: Create SprintDiscussionService

**New file:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/SprintDiscussionService.cs`

Follow the pattern of existing services (e.g., `CommentService.cs` or `SprintPlanningService.cs`). Key structure:

```csharp
namespace DotNetCloud.Modules.Tracks.Data.Services;

public sealed class SprintDiscussionService
{
    private readonly TracksDbContext _db;
    private readonly ITracksRealtimeService _realtimeService;
    private readonly ILogger<SprintDiscussionService> _logger;

    public SprintDiscussionService(
        TracksDbContext db,
        ITracksRealtimeService realtimeService,
        ILogger<SprintDiscussionService> logger)
    {
        _db = db;
        _realtimeService = realtimeService;
        _logger = logger;
    }

    // GET /api/v1/sprints/{sprintId}/discussions?skip=0&take=50
    public async Task<IReadOnlyList<SprintDiscussionDto>> GetSprintMessagesAsync(
        Guid sprintId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var messages = await _db.SprintDiscussions
            .AsNoTracking()
            .Where(m => m.SprintId == sprintId)
            .OrderBy(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(m => new SprintDiscussionDto(m.Id, m.SprintId, m.ReviewSessionId,
                m.UserId, m.UserDisplayName, m.Content, m.CreatedAt))
            .ToListAsync(ct);

        return messages;
    }

    // GET /api/v1/reviews/{reviewSessionId}/discussions?skip=0&take=50
    public async Task<IReadOnlyList<SprintDiscussionDto>> GetReviewSessionMessagesAsync(
        Guid reviewSessionId, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        // Same pattern as above but filter by ReviewSessionId
        // ...implementation...
    }

    // POST /api/v1/sprints/{sprintId}/discussions
    public async Task<SprintDiscussionDto> SendSprintMessageAsync(
        Guid sprintId, Guid userId, string userDisplayName, string content, CancellationToken ct = default)
    {
        // Validate: content required, max 2000
        if (string.IsNullOrWhiteSpace(content))
            throw new ValidationException("Content is required.");
        if (content.Length > 2000)
            throw new ValidationException("Content must be 2000 characters or fewer.");

        var message = new SprintDiscussion
        {
            SprintId = sprintId,
            UserId = userId,
            UserDisplayName = userDisplayName,
            Content = content.Trim()
        };

        _db.SprintDiscussions.Add(message);
        await _db.SaveChangesAsync(ct);

        var dto = new SprintDiscussionDto(message.Id, message.SprintId, message.ReviewSessionId,
            message.UserId, message.UserDisplayName, message.Content, message.CreatedAt);

        // Broadcast via SignalR
        await _realtimeService.BroadcastSprintDiscussionMessageAsync(sprintId, dto, ct);

        return dto;
    }

    // POST /api/v1/reviews/{reviewSessionId}/discussions
    public async Task<SprintDiscussionDto> SendReviewSessionMessageAsync(
        Guid reviewSessionId, Guid userId, string userDisplayName, string content, CancellationToken ct = default)
    {
        // Same pattern but sets ReviewSessionId
        // ...implementation...
    }
}
```

---

### Step 2.7: Create SprintDiscussionsController

**New file:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/SprintDiscussionsController.cs`

Follow the pattern of existing controllers (e.g., `CommentsController.cs`):

```csharp
namespace DotNetCloud.Modules.Tracks.Host.Controllers;

[ApiController]
public class SprintDiscussionsController : TracksControllerBase
{
    private readonly SprintDiscussionService _discussionService;
    private readonly IUserDirectory _userDirectory;  // for resolving display names
    private readonly ILogger<SprintDiscussionsController> _logger;

    public SprintDiscussionsController(
        SprintDiscussionService discussionService,
        IUserDirectory userDirectory,
        ILogger<SprintDiscussionsController> logger)
    {
        _discussionService = discussionService;
        _userDirectory = userDirectory;
        _logger = logger;
    }

    [HttpGet("api/v1/sprints/{sprintId:guid}/discussions")]
    public async Task<IActionResult> ListSprintDiscussions(
        Guid sprintId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var messages = await _discussionService.GetSprintMessagesAsync(sprintId, skip, take, ct);
        return Ok(Envelope(messages));
    }

    [HttpPost("api/v1/sprints/{sprintId:guid}/discussions")]
    public async Task<IActionResult> SendSprintDiscussion(
        Guid sprintId, [FromBody] SendSprintDiscussionDto dto, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var user = await _userDirectory.GetUserAsync(caller.UserId, ct);
        var displayName = user?.DisplayName ?? "Unknown";

        try
        {
            var message = await _discussionService.SendSprintMessageAsync(
                sprintId, caller.UserId, displayName, dto.Content, ct);
            return Created($"/api/v1/sprints/{sprintId}/discussions", Envelope(message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }

    [HttpGet("api/v1/reviews/{reviewSessionId:guid}/discussions")]
    public async Task<IActionResult> ListReviewDiscussions(
        Guid reviewSessionId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var messages = await _discussionService.GetReviewSessionMessagesAsync(reviewSessionId, skip, take, ct);
        return Ok(Envelope(messages));
    }

    [HttpPost("api/v1/reviews/{reviewSessionId:guid}/discussions")]
    public async Task<IActionResult> SendReviewDiscussion(
        Guid reviewSessionId, [FromBody] SendSprintDiscussionDto dto, CancellationToken ct = default)
    {
        var caller = GetAuthenticatedCaller();
        var user = await _userDirectory.GetUserAsync(caller.UserId, ct);
        var displayName = user?.DisplayName ?? "Unknown";

        try
        {
            var message = await _discussionService.SendReviewSessionMessageAsync(
                reviewSessionId, caller.UserId, displayName, dto.Content, ct);
            return Created($"/api/v1/reviews/{reviewSessionId}/discussions", Envelope(message));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ErrorEnvelope(ErrorCodes.ValidationError, ex.Message));
        }
    }
}
```

> **Note:** If the controller pattern uses a different base class or error handling approach, match the existing controllers exactly. Look at `CommentsController.cs` as the reference.

---

### Step 2.8: Add real-time broadcast methods

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksRealtimeService.cs`

Add these two methods to the interface:

```csharp
/// <summary>Broadcasts a new discussion message to all sprint planning participants.</summary>
Task BroadcastSprintDiscussionMessageAsync(Guid sprintId, SprintDiscussionDto message, CancellationToken cancellationToken = default);

/// <summary>Broadcasts a new discussion message to all review session participants.</summary>
Task BroadcastReviewDiscussionMessageAsync(Guid reviewSessionId, SprintDiscussionDto message, CancellationToken cancellationToken = default);
```

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksRealtimeService.cs`

Implement both methods. Follow the existing pattern of other broadcast methods in this class. Use SignalR groups:

- Sprint discussion group: `sprint-discussion-{sprintId}`
- Review discussion group: `review-discussion-{reviewSessionId}`

The implementation should call `IRealtimeBroadcaster.SendAsync(groupName, "DiscussionMessageReceived", dto)`.

---

### Step 2.9: Add SignalR events for UI components

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksSignalRService.cs`

Add these two events:

```csharp
/// <summary>Raised when a discussion message is received for a sprint.</summary>
event Action<Guid, SprintDiscussionDto>? SprintDiscussionMessageReceived;

/// <summary>Raised when a discussion message is received for a review session.</summary>
event Action<Guid, SprintDiscussionDto>? ReviewDiscussionMessageReceived;
```

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksInProcessSignalRService.cs`

Add backing fields and raise these events when the corresponding SignalR messages arrive. Follow the pattern of existing events in this class.

---

### Step 2.10: Add ITracksApiClient methods for discussions

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/ITracksApiClient.cs`

Add these four methods:

```csharp
Task<IReadOnlyList<SprintDiscussionDto>> ListSprintDiscussionsAsync(Guid sprintId, int skip = 0, int take = 50, CancellationToken ct = default);
Task<SprintDiscussionDto?> SendSprintDiscussionAsync(Guid sprintId, string content, CancellationToken ct = default);
Task<IReadOnlyList<SprintDiscussionDto>> ListReviewDiscussionsAsync(Guid reviewSessionId, int skip = 0, int take = 50, CancellationToken ct = default);
Task<SprintDiscussionDto?> SendReviewDiscussionAsync(Guid reviewSessionId, string content, CancellationToken ct = default);
```

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/Services/TracksApiClient.cs`

Implement the 4 methods following the existing HTTP call patterns in this file. All calls use `_httpClient` with JSON serialization.

**File:** `src/Core/DotNetCloud.Core.Server/Grpc/Clients/TracksGrpcApiClient.cs`

Implement the 4 methods using gRPC. Follow the existing pattern of other methods in this class. The gRPC client wraps calls with `SafeCallAsync<T>()` for error handling.

---

### Step 2.11: Add discussion RPCs to proto and gRPC service

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Protos/tracks_service.proto`

Add after the Sprint Planning RPC section (after `rpc AdjustSprint`):

```protobuf
  // ─── Sprint & Review Discussions ───
  rpc ListSprintDiscussions (ListSprintDiscussionsRequest) returns (ListSprintDiscussionsResponse);
  rpc SendSprintDiscussion (SendSprintDiscussionRequest) returns (SprintDiscussionMessage);
  rpc ListReviewDiscussions (ListReviewDiscussionsRequest) returns (ListReviewDiscussionsResponse);
  rpc SendReviewDiscussion (SendReviewDiscussionRequest) returns (SprintDiscussionMessage);
```

Add message types at the end of the file (before any closing brace):

```protobuf
// ─── Sprint & Review Discussion Messages ───

message SprintDiscussionMessage {
  string id = 1;
  string sprint_id = 2;
  string review_session_id = 3;
  string user_id = 4;
  string user_display_name = 5;
  string content = 6;
  google.protobuf.Timestamp created_at = 7;
}

message ListSprintDiscussionsRequest {
  string sprint_id = 1;
  int32 skip = 2;
  int32 take = 3;
}

message ListSprintDiscussionsResponse {
  repeated SprintDiscussionMessage messages = 1;
}

message SendSprintDiscussionRequest {
  string sprint_id = 1;
  string content = 2;
}

message ListReviewDiscussionsRequest {
  string review_session_id = 1;
  int32 skip = 2;
  int32 take = 3;
}

message ListReviewDiscussionsResponse {
  repeated SprintDiscussionMessage messages = 1;
}

message SendReviewDiscussionRequest {
  string review_session_id = 1;
  string content = 2;
}
```

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Services/TracksGrpcService.cs`

Add the 4 RPC implementations. Each delegates to `SprintDiscussionService`. Follow the pattern of existing RPC methods in this class for error handling, DTO mapping, and response building.

---

### Step 2.12: Add chat panel to SprintPlanningView.razor

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/SprintPlanningView.razor`

The current layout has two panels (`tracks-planning-panels`): Backlog (left) and Sprint Backlog (center). Add a third panel on the right for the discussion.

**Layout change:** Change the CSS grid from `grid-template-columns: 1fr 1fr` to `grid-template-columns: 1fr 1fr 320px` (or adjust based on current layout).

**Discussion panel markup** to add inside the `tracks-planning-panels` div, as the third child:

```razor
@* ── Right Panel: Discussion ── *@
<div class="tracks-planning-panel discussion">
    <div class="tracks-planning-panel-header">
        <h3>💬 Discussion</h3>
        <span class="tracks-muted">@_discussionMessages.Count messages</span>
    </div>

    @* Messages List *@
    <div class="tracks-discussion-messages" @ref="_discussionScrollRef">
        @if (_discussionMessages.Count == 0)
        {
            <p class="tracks-muted discussion-empty">No messages yet. Start the discussion!</p>
        }
        else
        {
            @foreach (var msg in _discussionMessages)
            {
                <div class="tracks-discussion-message">
                    <div class="tracks-discussion-message-header">
                        <span class="tracks-discussion-author">@msg.UserDisplayName</span>
                        <span class="tracks-discussion-time">@GetRelativeTime(msg.CreatedAt)</span>
                    </div>
                    <div class="tracks-discussion-content">@msg.Content</div>
                </div>
            }
        }
    </div>

    @* Input *@
    <div class="tracks-discussion-input">
        <textarea class="form-control"
                  @bind="_discussionInput"
                  @onkeydown="HandleDiscussionKeyDown"
                  placeholder="Type a message..."
                  rows="2"
                  maxlength="2000"
                  disabled="@_isSending"></textarea>
        <button class="btn btn-sm btn-primary"
                @onclick="SendDiscussionMessageAsync"
                disabled="@(string.IsNullOrWhiteSpace(_discussionInput) || _isSending)">
            Send
        </button>
    </div>
</div>
```

**CSS additions** (add to the existing scoped CSS file or inline):

```css
.tracks-discussion-messages {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 8px 0;
}

.tracks-discussion-message {
  padding: 6px 8px;
  border-radius: 6px;
  background: var(--bg-secondary, #f3f4f6);
}

.tracks-discussion-message-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 2px;
  font-size: 0.8rem;
}

.tracks-discussion-author {
  font-weight: 600;
}

.tracks-discussion-time {
  color: var(--text-muted, #9ca3af);
}

.tracks-discussion-content {
  font-size: 0.9rem;
  white-space: pre-wrap;
  word-break: break-word;
}

.tracks-discussion-input {
  display: flex;
  gap: 6px;
  padding-top: 8px;
  border-top: 1px solid var(--border, #e5e7eb);
}

.tracks-discussion-input textarea {
  flex: 1;
  resize: none;
}
```

---

### Step 2.13: Add discussion logic to SprintPlanningView.razor.cs

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/SprintPlanningView.razor.cs`

Add these fields and methods:

```csharp
// Discussion state
private readonly List<SprintDiscussionDto> _discussionMessages = new();
private string _discussionInput = string.Empty;
private bool _isSending;
private ElementReference _discussionScrollRef;
private IDisposable? _discussionSubscription;
private Timer? _discussionPollTimer;

// Inject ITracksSignalRService and ITracksApiClient (if not already injected)
// [Inject] ITracksSignalRService SignalRService { get; set; } = default!;

protected override async Task OnInitializedAsync()
{
    // ... existing initialization ...

    // Load discussion messages
    await LoadDiscussionMessagesAsync();

    // Subscribe to real-time discussion updates
    if (SignalRService.IsActive)
    {
        _discussionSubscription = /* subscribe to SignalRService.SprintDiscussionMessageReceived */;
    }
    else
    {
        // Polling fallback: poll every 10 seconds
        _discussionPollTimer = new Timer(async _ => await LoadDiscussionMessagesAsync(),
            null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }
}

private async Task LoadDiscussionMessagesAsync()
{
    try
    {
        var messages = await ApiClient.ListSprintDiscussionsAsync(Sprint.Id, ct: CancellationToken.None);
        _discussionMessages.Clear();
        _discussionMessages.AddRange(messages);
        await InvokeAsync(StateHasChanged);
        await ScrollToDiscussionBottom();
    }
    catch (Exception ex)
    {
        // Log but don't disrupt the planning UI
        _logger.LogDebug(ex, "Failed to load sprint discussion messages");
    }
}

private async Task SendDiscussionMessageAsync()
{
    if (string.IsNullOrWhiteSpace(_discussionInput) || _isSending)
        return;

    _isSending = true;
    try
    {
        await ApiClient.SendSprintDiscussionAsync(Sprint.Id, _discussionInput.Trim());
        _discussionInput = string.Empty;
        await LoadDiscussionMessagesAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to send discussion message");
        // Show a toast or inline error
    }
    finally
    {
        _isSending = false;
    }
}

private void HandleDiscussionMessageReceived(Guid sprintId, SprintDiscussionDto message)
{
    if (sprintId != Sprint.Id) return;
    _discussionMessages.Add(message);
    InvokeAsync(StateHasChanged);
    _ = ScrollToDiscussionBottom();
}

private async Task HandleDiscussionKeyDown(KeyboardEventArgs e)
{
    if (e.Key == "Enter" && !e.ShiftKey)
    {
        await SendDiscussionMessageAsync();
    }
}

private async Task ScrollToDiscussionBottom()
{
    // Use JS interop to scroll the discussion container to bottom
    // await JsRuntime.InvokeVoidAsync("scrollToBottom", _discussionScrollRef);
}

private string GetRelativeTime(DateTime dateTime)
{
    var diff = DateTime.UtcNow - dateTime;
    if (diff.TotalSeconds < 60) return "just now";
    if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
    if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
    return dateTime.ToString("MMM d");
}

public void Dispose()
{
    _discussionSubscription?.Dispose();
    _discussionPollTimer?.Dispose();
}
```

---

### Step 2.14: Add chat panel to ReviewSessionHost.razor

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionHost.razor`

The current layout has a left card area and a right sidebar with Participants and Poker sections. Add a "💬 Discussion" section below the Poker section in the right sidebar. Use the same markup pattern as the SprintPlanningView discussion panel, but scoped to `Session.Id` and using `ListReviewDiscussionsAsync` / `SendReviewDiscussionAsync`.

---

### Step 2.15: Add chat panel to ReviewSessionParticipant.razor

**File:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks/UI/ReviewSessionParticipant.razor`

Same as ReviewSessionHost — add the discussion panel to the right sidebar below the Poker section. The participant sees and interacts with the same messages. Scoped to `Session.Id`.

---

## Phase 3: Tests

### Step 3.1: SprintDiscussionServiceTests

**New file:** `tests/DotNetCloud.Modules.Tracks.Tests/SprintDiscussionServiceTests.cs`

Follow the test patterns from existing test files (e.g., `CommentServiceTests.cs`, `SprintPlanningServiceTests.cs`). Use in-memory database for isolation. Use Moq for `ITracksRealtimeService`.

Test methods:

```csharp
[TestClass]
public class SprintDiscussionServiceTests
{
    // 1. SendSprintMessage_ValidContent_CreatesMessage
    //    - Create a sprint in the in-memory DB
    //    - Call SendSprintMessageAsync
    //    - Verify: message persisted with correct SprintId, UserId, Content

    // 2. SendSprintMessage_EmptyContent_ThrowsValidationException
    //    - Call SendSprintMessageAsync with ""
    //    - Verify: ValidationException thrown

    // 3. SendSprintMessage_ContentTooLong_ThrowsValidationException
    //    - Call SendSprintMessageAsync with 2001 chars
    //    - Verify: ValidationException thrown

    // 4. GetSprintMessages_Paginated_ReturnsCorrectPage
    //    - Create 10 messages
    //    - Fetch skip=0, take=5
    //    - Verify: 5 returned, ordered by CreatedAt ascending

    // 5. GetSprintMessages_EmptySprint_ReturnsEmptyList
    //    - Fetch messages from sprint with no messages
    //    - Verify: empty list returned

    // 6. SendReviewSessionMessage_ValidContent_CreatesMessage
    //    - Create a review session
    //    - Call SendReviewSessionMessageAsync
    //    - Verify: message persisted with correct ReviewSessionId

    // 7. GetReviewSessionMessages_OrderedByCreatedAt
    //    - Create 3 messages with 1-second gaps
    //    - Fetch all
    //    - Verify: returned in chronological order (oldest first)

    // 8. SendSprintMessage_BroadcastsRealtimeEvent
    //    - Mock ITracksRealtimeService
    //    - Call SendSprintMessageAsync
    //    - Verify: BroadcastSprintDiscussionMessageAsync was called once with correct args
}
```

---

### Step 3.2: TracksGrpcApiClientTests

**New file:** `tests/DotNetCloud.Modules.Tracks.Tests/TracksGrpcApiClientTests.cs`

Test the gRPC client methods for discussions:

```csharp
[TestClass]
public class TracksGrpcApiClientTests
{
    // 1. ListSprintDiscussions_DelegatesToGrpc
    //    - Mock the generated gRPC client
    //    - Call ListSprintDiscussionsAsync
    //    - Verify: correct gRPC method called with correct request

    // 2. SendSprintDiscussion_DelegatesToGrpc
    //    - Mock gRPC client
    //    - Call SendSprintDiscussionAsync
    //    - Verify: correct RPC called

    // 3. GrpcUnavailable_ReturnsFallback
    //    - Mock gRPC client to throw RpcException(StatusCode.Unavailable)
    //    - Call ListSprintDiscussionsAsync
    //    - Verify: returns empty list (graceful fallback), logs warning
}
```

---

### Step 3.3: Run existing tests — verify no regressions

```bash
dotnet test tests/DotNetCloud.Modules.Tracks.Tests/ -c Release
```

All 21 existing test files must pass. Common breakage points:

- Tests that depend on Core.Server in-process service registration may need `TracksApiClient` mocked instead
- Tests that reference removed namespaces need using statement updates

---

## Phase 4: Documentation Cleanup

### Step 4.1: Update TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md

**File:** `docs/TRACKS_COMPREHENSIVE_FEATURE_ANALYSIS.md`

1. **Add a banner at the top** (after the title block):

```markdown
> ⚠️ **Note:** This document was written April 29, 2026, before the professionalization plan was executed.
> Most features listed as MISSING below are now implemented. See `TRACKS_PROFESSIONALIZATION_PLAN.md`
> and `TRACKS_REMAINING_GAPS_PLAN.md` for current status.
> **Last reviewed:** 2026-08-02
```

2. **Update the Feature Matrix table** — change these from ❌ to ✅:
   - Table/List View
   - Calendar View
   - Roadmaps
   - Custom Views/Filters
   - Dashboards
   - Goals/OKRs
   - Custom Fields
   - Workflows/Automation
   - Watchers/Subscribers
   - Webhooks/Integrations
   - Keyboard Shortcuts
   - Command Palette
   - Undo/Undo Toast
   - Import/Export (CSV)

   Change @mentions from ❌ (partial) to ✅.
   Change Search from ❌ to ✅ (full-text search via Search module integration).

3. **Update the Detailed Gap Analysis sections** — for each section that's now implemented, add:

```markdown
> **Status:** ✅ Implemented — see `docs/TRACKS_PROFESSIONALIZATION_PLAN.md` for details.
```

---

### Step 4.2: Update TRACKS_HIERARCHY_EXPANSION.md header

**File:** `docs/TRACKS_HIERARCHY_EXPANSION.md`

Change line 3 from:

```markdown
> **Status:** Implemented — source code clean, UI + tests pending
```

To:

```markdown
> **Status:** ✅ Complete — data model, services, API, and UI fully implemented.
> Tests: `tests/DotNetCloud.Modules.Tracks.Tests/` (21 test files)
```

---

### Step 4.3: Update IMPLEMENTATION_CHECKLIST.md

**File:** `docs/IMPLEMENTATION_CHECKLIST.md`

1. Find the Tracks section and add/update these entries as `✓`:

```markdown
#### Tracks Module — Process Isolation

- ✓ Remove dual ITracksApiClient registration from Core.Server Program.cs
- ✓ Remove in-process project references from Core.Server.csproj
- ✓ Verify gRPC-only communication

#### Tracks Module — Sprint Planning Chat

- ✓ SprintDiscussion entity + EF configuration + migrations
- ✓ SprintDiscussionService (CRUD + real-time broadcast)
- ✓ SprintDiscussionsController (REST API)
- ✓ gRPC RPCs + TracksGrpcService implementations
- ✓ ITracksApiClient discussion methods (HTTP + gRPC)
- ✓ SprintPlanningView discussion panel
- ✓ ReviewSessionHost discussion panel
- ✓ ReviewSessionParticipant discussion panel
- ✓ ITracksRealtimeService broadcast methods
- ✓ ITracksSignalRService discussion events
```

---

### Step 4.4: Update MASTER_PROJECT_PLAN.md

**File:** `docs/MASTER_PROJECT_PLAN.md`

1. **Quick Status Summary** — update the Tracks rows:

```markdown
| Tracks — Phase 4.10 Hierarchy | 17 | 17 | 0 | 0 |
| Tracks — gRPC Isolation | 3 | 3 | 0 | 0 |
| Tracks — Sprint Planning Chat | 15 | 15 | 0 | 0 |
```

2. **Phase 4.1 DTO references fix** — change `BoardDto`/`CardDto`/`CardCommentDto` to `ProductDto`/`WorkItemDto`/`WorkItemCommentDto` in the deliverables section.

3. **Add a new step section** after Phase 4.10:

```markdown
### Step: phase-4.11 — gRPC Process Isolation & Sprint Planning Chat

**Status:** completed ✅
**Duration:** ~6 hours
**Description:** Fix gRPC dual-registration bug and add real-time chat to sprint planning and review sessions.

**Deliverables:**

- ✓ Removed dual ITracksApiClient HTTP registration from Core.Server Program.cs
- ✓ Removed in-process Tracks project references from Core.Server.csproj
- ✓ SprintDiscussion entity with EF configuration and dual-provider migrations
- ✓ SprintDiscussionService with CRUD, validation, and real-time SignalR broadcast
- ✓ SprintDiscussionsController with 4 REST endpoints
- ✓ 4 new gRPC RPCs in tracks_service.proto with TracksGrpcService implementations
- ✓ Discussion panel in SprintPlanningView.razor (right sidebar)
- ✓ Discussion panel in ReviewSessionHost.razor (sidebar below poker)
- ✓ Discussion panel in ReviewSessionParticipant.razor (sidebar below poker)
- ✓ ITracksRealtimeService broadcast methods for sprint and review discussions
- ✓ ITracksSignalRService events for UI real-time updates
- ✓ 15 new unit tests across SprintDiscussionServiceTests + TracksGrpcApiClientTests

**Notes:** Sprint planning and review sessions now have real-time chat.
Tracks module is fully process-isolated via gRPC.
```

---

## Phase 5: Build & Verification

### Step 5.1: Full solution build

```bash
dotnet build DotNetCloud.CI.slnf -c Release
```

Must succeed with 0 errors.

**Common fix:** If `ITracksApiClient` or Tracks DTO types are referenced in Core.Server after removing project references, those usages need to:

1. Switch to `DotNetCloud.Core.Services.ModuleApis.ITracksApiClient` (the shared interface namespace)
2. Use gRPC-generated types instead of Tracks-specific DTOs

---

### Step 5.2: Run Tracks tests

```bash
dotnet test tests/DotNetCloud.Modules.Tracks.Tests/ -c Release
```

All tests pass (existing + new).

---

### Step 5.3: Run full test suite

```bash
dotnet test -c Release
```

Ensure no regressions across all modules.

---

### Step 5.4: Deploy and smoke test

```bash
sudo ./scripts/deploy.sh
```

Verify:

1. `systemctl status dotnetcloud` — Tracks module process starts and passes health check
2. Open Tracks in browser — products, work items, sprints all load
3. Open a sprint → Sprint Planning view → discussion panel appears on right
4. Type a message → appears in real-time
5. Start a Review Session → discussion panel appears in sidebar
6. Start poker during review → poker and chat coexist in sidebar
7. Sprint completion dialog still works
8. All existing Tracks features still function

---

## Implementation Order

```
Phase 1 (gRPC fix) ──> Build verify ──> Phase 2 (Sprint chat) ──> Build verify
                                                                    │
                                                                    v
                                            Phase 3 (Tests) ──> Phase 4 (Docs) ──> Phase 5 (Deploy + smoke test)
```

### What can run in parallel:

- Phase 2 steps 2.1-2.3 (entity/migration) can be done in parallel with steps 2.8-2.9 (realtime interfaces)
- Phase 2 steps 2.12-2.15 (3 UI panels) can be done in parallel
- Phase 3 tests (3 test files) can be written in parallel
- Phase 4 docs (4 files) can be updated in parallel with Phase 3 tests

---

## Design Decisions

| Decision                                                                | Rationale                                                                       |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| `SprintDiscussion` uses dual nullable FKs (SprintId OR ReviewSessionId) | Simpler to query than polymorphic discriminator; clear ownership per scope      |
| `UserDisplayName` is snapshotted at post time                           | Avoids cross-module gRPC lookup on every message render; tolerates name changes |
| No edit/delete for discussion messages                                  | Ephemeral discussion, not permanent records. Keeps implementation simple        |
| Keep `.Data.SqlServer` in Core.Server.csproj                            | Required for `dotnet ef` migration tooling even after process isolation         |
| Chat and poker share the same discussion panel in review sessions       | Single conversation context — no need for separate poker chat                   |

## Risks & Mitigations

| Risk                                                                                     | Mitigation                                                                                                                     |
| ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Removing `AddTracksUiServices` breaks Blazor UI if Core.Server renders Tracks components | Before removal, verify how Tracks UI is served. If server-rendered, keep DbContext registration only (strip business services) |
| Existing tests break from removed project references                                     | Run tests early and often. Fix usings and mock setups incrementally                                                            |
| gRPC `TracksGrpcApiClient` is missing methods needed by Core.Server                      | Audit all `ITracksApiClient` usages in Core.Server before removing HTTP registration. Add missing gRPC methods first           |
| Migration conflicts if SprintDiscussion table already exists                             | Check for existing tables with `rg "SprintDiscussion" src/Modules/Tracks/` — should return nothing since this is new           |

## Further Considerations

1. **`TracksGrpcApiClient` completeness check** — Verify it implements ALL methods from `ITracksApiClient`. Run: `rg "public.*Task" src/Core/DotNetCloud.Core.Server/Grpc/Clients/TracksGrpcApiClient.cs` and compare against `ITracksApiClient.cs`. Missing methods must be added before removing the HTTP registration.

2. **`TRACKS_REMAINING_GAPS_PLAN.md` verification** — Phase E deliverables (Comment Reactions, Guest Access) are listed with ☐ unchecked in that plan but entities exist in code. Verify actual implementation status of CommentReaction, GuestUser, GuestPermission, WorkItemShareLink before updating doc status.

3. **TemplateSeedService** — The Tracks Host `Program.cs` calls `TemplateSeedService.EnsureSeededAsync()` on startup. Verify this service is registered in the Host's DI (it should be since it's in `AddTracksServices`). After isolation, this runs in the Tracks Host process, not Core.Server — correct behavior.

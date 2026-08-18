# Plan: Remove EF Core InMemory provider from all non-test code

## Objective

Remove every `UseInMemoryDatabase(...)` call and every `Microsoft.EntityFrameworkCore.InMemory`
package reference from **production code** (`src/`), and rewrite the **Example** module to the
canonical dual-provider (PostgreSQL + SQL Server) pattern. Test projects (`tests/`) keep using
the InMemory provider unchanged.

## Scope boundaries

**IN SCOPE (change these):**

- 12 module Host `Program.cs` files that call `UseInMemoryDatabase` (AI, Bookmarks, Calendar,
  Chat, Contacts, Email, Files, Music, Notes, Photos, Search, Tracks).
- 13 module Host `.csproj` files that reference `Microsoft.EntityFrameworkCore.InMemory`
  (the 12 above plus Video, whose reference is unused).
- The Example module (Host `Program.cs`, Host `.csproj`, Data `.csproj`, design-time factories,
  a new SQL Server migration set, and its README).
- README/doc files that document the old InMemory fallback behavior.

**OUT OF SCOPE (do NOT change):**

- `tests/**` — every test project keeps `UseInMemoryDatabase` (they inject it via their own
  `WebApplicationFactory` subclasses, not via the Host `Program.cs`).
- `Directory.Packages.props` line 53 — keep the central `Microsoft.EntityFrameworkCore.InMemory`
  `10.0.10` version pin; tests still need it.
- `src/Modules/Chat/DotNetCloud.Modules.Chat/Services/InMemoryNotificationPreferenceStore.cs` —
  this is an in-process cache, NOT the EF provider. Leave it.
- `src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicEnrichmentBackgroundQueue.cs`
  (`InMemoryMusicEnrichmentBackgroundQueue`) — in-process queue, NOT the EF provider. Leave it.
- `src/Core/DotNetCloud.Core.Server/Program.cs` — the core server already uses
  `UseNpgsql`/`UseSqlServer` only; no InMemory there.
- `src/CLI/DotNetCloud.CLI/Infrastructure/ServiceProviderFactory.cs` — no change. Example is
  `schemaProvider: "self"`, so the core does not migrate it.

## Why (context for the implementer)

Every module Host `Program.cs` currently does:

```csharp
if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    // real provider (UseNpgsql / UseSqlServer)
}
else
{
    // UseInMemoryDatabase("XxxModule")  ← silent fallback
}
```

In production this `else` is dead code: `ProcessSupervisor` forwards `DOTNETCLOUD_CONFIG_DIR`
(`src/Core/DotNetCloud.Core.Server/Supervisor/ProcessSupervisor.cs` around line 397), each host
loads `config.json` from that dir, and `config.json` always contains `connectionString` +
`databaseProvider` (the CLI setup enforces it). The fallback only fires when a host is run
standalone with no database config.

The goal is to make the modules **fail fast** (throw) when the database config is missing,
instead of silently booting with an empty in-memory database and reporting healthy.

## Conventions to reuse (do not reinvent)

- **Provider branch:** compare `dbProvider` case-insensitively to `"PostgreSql"`; use
  `UseNpgsql` for PostgreSQL, `UseSqlServer` for anything else (SQL Server).
- **Naming strategy:** register `PostgreSqlNamingStrategy` for PostgreSQL,
  `SqlServerNamingStrategy` for SQL Server. Both live in `DotNetCloud.Core.Data.Naming`.
- **Dual-provider migrations (one Data assembly):** PostgreSQL migrations live in
  `Migrations/` (namespace `Xxx.Data.Migrations`); SQL Server migrations live in
  `Migrations/SqlServer/` (namespace `Xxx.Data.SqlServer.Migrations`). At runtime use
  `options.ReplaceService<IMigrationsAssembly, DotNetCloud.Core.Data.Infrastructure.ProviderAwareMigrationsAssembly>()`
  to filter migrations by provider. Reference example: `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs`
  and `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/`.
- **Fail-fast guard** uses `InvalidOperationException` (no extra `using` needed; implicit usings
  cover `System`).

---

## Phase 1 — Mechanical removal from the 12 module hosts

These 12 hosts all follow the same shape. Do them identically (they are independent and can be
done in parallel).

### 1a. Uniform code transform

For each host, locate this pattern:

```csharp
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(dbProvider))
{
    // ... real provider registration (KEEP THIS BODY, do not edit it) ...
}
else
{
    builder.Services.AddDbContext<XxxDbContext>(options =>
        options.UseInMemoryDatabase("XxxModule"));
}
```

Change it to:

```csharp
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
{
    throw new InvalidOperationException(
        "The <Module> module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

// ... real provider registration (same body that used to be inside the `if`) ...
```

In other words:

1. Flip the `if` condition to the negated check and `throw` inside it.
2. Delete the entire `else { ... UseInMemoryDatabase(...) ... }` block.
3. The former `if`-body becomes unconditional top-level code — do NOT re-indent or otherwise
   alter it beyond removing the wrapping `if { }` and its closing brace.
4. Use the module's real name in the exception message (e.g., "The Files module requires ...").

### 1b. Per-module reference (file + quirks)

For each module: edit the Host `Program.cs` as above AND remove the
`<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />` line from the matching
`.csproj`.

| # | Module | Program.cs (DB section approx. lines) | DbContext(s) in the `if` | Quirks to watch |
|---|---|---|---|---|
| 1 | AI | `src/Modules/AI/DotNetCloud.Modules.AI.Host/Program.cs` (~84–103) | `AiDbContext` | Simple single DbContext. |
| 2 | Bookmarks | `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Program.cs` (~89–109) | `BookmarksDbContext` | Simple. `else` has a `// Fallback to in-memory...` comment — delete it with the else. |
| 3 | Calendar | `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs` (~128–157) | `CalendarDbContext` (+ `ReplaceService<IMigrationsAssembly, ProviderAwareMigrationsAssembly>`) | Naming strategy is registered INSIDE the `if` (keep it). After the if/else there are more unconditional registrations (`ContactsDbContext`, `CoreDbContext`) that use `dbProvider` directly — leave them untouched. |
| 4 | Chat | `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Program.cs` (~116–147) | `ChatDbContext` (via a local `configureChatDb` delegate) + `ChatDbContextFactory` | Naming strategy is registered BEFORE the if/else (keep it where it is). Keep the `configureChatDb` delegate and the `ChatDbContextFactory` registration; delete only the `else` block. |
| 5 | Contacts | `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/Program.cs` (~98–121) | `ContactsDbContext`, `CalendarDbContext`, `NotesDbContext` (via local `ConfigureDb` delegate) | Delete the `else` that registers the three InMemory contexts. |
| 6 | Email | `src/Modules/Email/DotNetCloud.Modules.Email.Host/Program.cs` (~90–116) | `EmailDbContext` | Naming strategy inside the `if` (keep it). |
| 7 | Files | `src/Modules/Files/DotNetCloud.Modules.Files.Host/Program.cs` (~124–159) | `FilesDbContext` + transient `CoreDbContext` + `ITableNamingStrategy` | The `else` also registers a transient `CoreDbContext` InMemory and a `PostgreSqlNamingStrategy` — delete the whole `else`. The `if` already registers both DbContexts + naming strategy. |
| 8 | Music | `src/Modules/Music/DotNetCloud.Modules.Music.Host/Program.cs` (~120–161) | `MusicDbContext` (factory + context), `FilesDbContext` (factory + context), `IFileStorageEngine`, `ITableNamingStrategy` | The `else` registers a `PostgreSqlNamingStrategy` + InMemory `MusicDbContext` factory/context — delete the whole `else`. The `if` already registers naming strategy + all DbContexts + storage. |
| 9 | Notes | `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Program.cs` (~109–129) | `NotesDbContext` | Simple. |
| 10 | Photos | `src/Modules/Photos/DotNetCloud.Modules.Photos.Host/Program.cs` (~85–122) | `PhotosDbContext`, `FilesDbContext` (factory + context), `IFileStorageEngine`, `ITableNamingStrategy` | Delete the `else`. The `if` already registers everything needed. |
| 11 | Search | `src/Modules/Search/DotNetCloud.Modules.Search.Host/Program.cs` (~91–111) | `SearchDbContext` | Simple. Note: later in the file `ResolveDatabaseProvider(...)` already throws when the provider is missing — so the DbContext guard just brings the two in line. |
| 12 | Tracks | `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Program.cs` (~88–108) | `TracksDbContext` | Simple. |

### 1c. csproj reference removal (13 files)

Delete this exact line from each:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
```

Files:

- `src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj` (line 24)
- `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/DotNetCloud.Modules.Bookmarks.Host.csproj` (line 20)
- `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj` (line 23)
- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/DotNetCloud.Modules.Chat.Host.csproj` (line 24)
- `src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/DotNetCloud.Modules.Contacts.Host.csproj` (line 23)
- `src/Modules/Email/DotNetCloud.Modules.Email.Host/DotNetCloud.Modules.Email.Host.csproj` (line 20)
- `src/Modules/Example/DotNetCloud.Modules.Example.Host/DotNetCloud.Modules.Example.Host.csproj` (line 14) — handled in Phase 2
- `src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj` (line 27)
- `src/Modules/Music/DotNetCloud.Modules.Music.Host/DotNetCloud.Modules.Music.Host.csproj` (line 23)
- `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/DotNetCloud.Modules.Notes.Host.csproj` (line 23)
- `src/Modules/Photos/DotNetCloud.Modules.Photos.Host/DotNetCloud.Modules.Photos.Host.csproj` (line 23)
- `src/Modules/Search/DotNetCloud.Modules.Search.Host/DotNetCloud.Modules.Search.Host.csproj` (line 23)
- `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/DotNetCloud.Modules.Tracks.Host.csproj` (line 23)

---

## Phase 2 — Example module → canonical dual-provider pattern

Decision (confirmed with user): **full dual-provider** — Example gets SQL Server support
(migration set + design-time factory + provider-aware runtime), matching the real modules.

The Example module is the reference template. Its current `Program.cs` diverges from the real
modules in two ways that this phase fixes:

1. It reads `DOTNETCLOUD_CONNECTION_STRING` (which `ProcessSupervisor` never sets) and does not
   load `config.json` from `DOTNETCLOUD_CONFIG_DIR`.
2. It is PostgreSQL-only (Npgsql + hard-coded `PostgreSqlNamingStrategy`, no SQL Server path).

### 2a. `src/Modules/Example/DotNetCloud.Modules.Example.Host/DotNetCloud.Modules.Example.Host.csproj`

- Remove: `<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />` (line 14).
- Add: `<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />` (next to the
  existing `Npgsql.EntityFrameworkCore.PostgreSQL` reference).

### 2b. `src/Modules/Example/DotNetCloud.Modules.Example.Host/Program.cs`

Replace the DB wiring (currently lines ~16–33, the block that reads `DOTNETCLOUD_CONNECTION_STRING`
and branches to `UseInMemoryDatabase`) with the canonical pattern. Final result should be
(adjust surrounding code minimally):

```csharp
var builder = WebApplication.CreateBuilder(args);

// Load shared config from DOTNETCLOUD_CONFIG_DIR (set by ProcessSupervisor)
var configDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
if (!string.IsNullOrEmpty(configDir))
{
    var configPath = Path.Combine(configDir, "config.json");
    if (File.Exists(configPath))
        builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
}

// Register the Example module as a singleton for lifecycle management
builder.Services.AddSingleton<ExampleModule>();

// Register EF Core with the configured database provider (no in-memory fallback)
var connectionString = builder.Configuration["connectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var dbProvider = builder.Configuration["databaseProvider"]
    ?? builder.Configuration["database:provider"];

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(dbProvider))
{
    throw new InvalidOperationException(
        "The Example module requires a database connection string and provider. " +
        "These are provided via config.json (DOTNETCLOUD_CONFIG_DIR) when launched by the core server.");
}

var isPostgreSql = string.Equals(dbProvider, "PostgreSql", StringComparison.OrdinalIgnoreCase);

builder.Services.AddSingleton<ITableNamingStrategy>(isPostgreSql
    ? new PostgreSqlNamingStrategy()
    : new SqlServerNamingStrategy());

builder.Services.AddDbContext<ExampleDbContext>(options =>
{
    if (isPostgreSql)
    {
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "example"));
    }
    else
    {
        options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 3);
            sql.CommandTimeout(30);
            sql.MigrationsAssembly(typeof(ExampleDbContext).Assembly.FullName);
            sql.MigrationsHistoryTable("__EFMigrationsHistory", "example");
        });
    }

    options.ReplaceService<IMigrationsAssembly, DotNetCloud.Core.Data.Infrastructure.ProviderAwareMigrationsAssembly>();
});

// gRPC + health checks
builder.Services.AddGrpc();
builder.Services.AddHealthChecks().AddCheck<ExampleHealthCheck>("example_module");

var app = builder.Build();

// Self-migrate on startup (schemaProvider: "self").
// A connection string is now mandatory, so this is unconditional.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGrpcService<ExampleGrpcService>();
app.MapGrpcService<ExampleLifecycleService>();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { module = "dotnetcloud.example", version = "1.0.0", status = "running" }));

await app.RunAsync();
```

Required `using` additions (mirror Calendar Host): `Microsoft.EntityFrameworkCore.Infrastructure`
(for `ReplaceService`), `Microsoft.EntityFrameworkCore.Migrations` (for `IMigrationsAssembly`),
and `DotNetCloud.Core.Data.Infrastructure` (for `ProviderAwareMigrationsAssembly`). Confirm
against `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs`.

Remove the now-unused `DOTNETCLOUD_CONNECTION_STRING` logic and the old
`PostgreSqlNamingStrategy` unconditional registration (the old line
`builder.Services.AddSingleton<ITableNamingStrategy, PostgreSqlNamingStrategy>();`).

### 2c. `src/Modules/Example/DotNetCloud.Modules.Example.Data/DotNetCloud.Modules.Example.Data.csproj`

Ensure the Data project can build both design-time factories:

- Add `<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />`.
- Add `<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />` if it is not already
  available transitively (the existing `ExampleDbContextFactory` already calls `UseNpgsql`, so it
  currently comes transitively via `DotNetCloud.Core.Data` — verify with a build and add the
  explicit reference only if needed).

### 2d. Design-time factories (Data project)

Keep the existing `ExampleDbContextFactory` (Npgsql + `PostgreSqlNamingStrategy`) as the
PostgreSQL factory. Optionally rename class/file to `ExampleDbContextDesignTimeFactory` for
symmetry with the real modules — if you rename, update any references.

Add a new file `src/Modules/Example/DotNetCloud.Modules.Example.Data/ExampleDbContextSqlServerDesignTimeFactory.cs`
(mirror `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/CalendarDbContextSqlServerDesignTimeFactory.cs`):

```csharp
using System;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DotNetCloud.Modules.Example.Data;

public class ExampleDbContextSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<ExampleDbContext>
{
    public ExampleDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DOTNETCLOUD_DB_CONNECTION")
            ?? "Server=localhost;Database=dotnetcloud_example_dev;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ExampleDbContext>();

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            sqlOptions.CommandTimeout(30);
            sqlOptions.MigrationsAssembly(typeof(ExampleDbContextSqlServerDesignTimeFactory).Assembly.FullName);
        });

        return new ExampleDbContext(options.Options, new SqlServerNamingStrategy());
    }
}
```

### 2e. SQL Server migration set

Add a SQL Server initial migration under
`src/Modules/Example/DotNetCloud.Modules.Example.Data/Migrations/SqlServer/`, mirroring the
existing PostgreSQL `Migrations/20260501213827_InitialCreate.cs`. The SQL Server migration
MUST use namespace `DotNetCloud.Modules.Example.Data.SqlServer.Migrations` (so
`ProviderAwareMigrationsAssembly` filters it correctly) and must produce schema `example`,
table `Notes` with SQL Server types.

Preferred approach — generate with the EF CLI (run from the repo root):

```bash
DOTNETCLOUD_DB_CONNECTION='Server=localhost;Database=dotnetcloud_example_dev;Trusted_Connection=True;TrustServerCertificate=True' \
  dotnet ef migrations add InitialCreate_SqlServer \
    --project src/Modules/Example/DotNetCloud.Modules.Example.Data \
    --startup-project src/Modules/Example/DotNetCloud.Modules.Example.Data \
    --context DotNetCloud.Modules.Example.Data.ExampleDbContext \
    --output-dir Migrations/SqlServer
```

If `dotnet ef` cannot pick between the two design-time factories, temporarily delete/move the
PostgreSQL factory, generate, then restore it. Verify the generated file's namespace is
`DotNetCloud.Modules.Example.Data.SqlServer.Migrations`; if not, fix it manually.

Fallback — hand-author `Migrations/SqlServer/20260501213827_InitialCreate.cs` by translating the
PostgreSQL migration:

- `uuid` → `uniqueidentifier`
- `character varying(200)` → `nvarchar(200)`
- `character varying(10000)` → `nvarchar(max)` (or `nvarchar(10000)`)
- `timestamp with time zone` → `datetime2`
- `defaultValueSql: "CURRENT_TIMESTAMP"` → `defaultValueSql: "SYSUTCDATETIME()"`
- Keep schema `"example"`, table `"Notes"`, PK `"PK_Notes"`, index `"ix_example_notes_created_at"`
  (match the SqlServerNamingStrategy conventions used by Calendar's SQL Server migrations).
- Add a matching `20260501213827_InitialCreate.Designer.cs` and update
  `ExampleDbContextModelSnapshot` — best done by generating via `dotnet ef` rather than by hand.

### 2f. Example README

In `src/Modules/Example/README.md`:

- Remove the "In-Memory Fallback for Development" section and the "uses an in-memory database by
  default" sentences.
- Correct the false claim that `ProcessSupervisor` sets `DOTNETCLOUD_CONNECTION_STRING`; document
  that the host reads `config.json` via `DOTNETCLOUD_CONFIG_DIR` (same as other modules).
- Update "Running Locally" to say a connection string + provider are now required (via
  `config.json` or `appsettings.json` `ConnectionStrings:DefaultConnection`).
- Document the dual-provider migrations layout (`Migrations/` = PostgreSQL,
  `Migrations/SqlServer/` = SQL Server) and the two design-time factories.

No change to `src/Modules/Example/manifest.json` (`schemaProvider: "self"` stays).

---

## Phase 3 — Video module (unused package reference only)

`src/Modules/Video/DotNetCloud.Modules.Video.Host/Program.cs` does NOT call `UseInMemoryDatabase`
(it already throws when the provider is missing, via `ResolveDatabaseProvider`). It only has a
stray package reference.

- Delete `<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />` from
  `src/Modules/Video/DotNetCloud.Modules.Video.Host/DotNetCloud.Modules.Video.Host.csproj` (line 23).
- No `Program.cs` change.

---

## Phase 4 — Documentation / README cleanups (secondary)

Grep for InMemory references in docs/READMEs and update only those describing the now-removed
fallback:

- `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/README.md` — lines ~63 and ~77 mention
  "InMemory database by default for development" and list `Microsoft.EntityFrameworkCore.InMemory`
  as a dependency. Remove/replace those statements.
- `docs/REQUIRED_MODULES_AND_SCHEMA_SEPARATION_PLAN.md` line ~47 states module Hosts "all use
  `UseInMemoryDatabase`". This is a historical planning doc; optionally add a note that the
  fallback was removed (or leave it — flag to reviewer).
- Do NOT touch the many `docs/*.md` references to "EF InMemory" that describe test behavior
  (e.g., `docs/MASTER_PROJECT_PLAN.md` line ~1897) — tests still use InMemory.

---

## Phase 5 — Verification

Run in this order (bash; repo is on Linux here, but `dotnet` commands are cross-platform):

1. **Confirm no InMemory remains in src:**

   ```bash
   rg -n "UseInMemoryDatabase" src/ || echo "OK: none in src"
   rg -n 'Microsoft\.EntityFrameworkCore\.InMemory' src/ --glob '*.csproj' || echo "OK: no csproj refs in src"
   ```

   Expected: zero matches in `src/`. (Tests will still match — that is correct.)

2. **Build the CI solution filter** (avoids Android SDK requirements):

   ```bash
   dotnet build DotNetCloud.CI.slnf -c Release
   ```

   Expected: success, no warnings/errors. Wait for it to finish (it can take several minutes).

3. **Run the Example module tests** (unaffected, but confirms no regression in the Example core
   project):

   ```bash
   dotnet test tests/DotNetCloud.Modules.Example.Tests
   ```

4. **Spot-check a couple of hosts for the guard** (sanity read):

   ```bash
   rg -n "requires a database connection string and provider" src/Modules
   ```

   Expected: 13 matches (12 mechanical hosts + Example).

5. **(Manual, optional)** Run one host standalone without DB config and confirm it throws the
   `InvalidOperationException` instead of booting with InMemory:

   ```bash
   DOTNETCLOUD_CONFIG_DIR=/nonexistent dotnet run --project src/Modules/Notes/DotNetCloud.Modules.Notes.Host
   ```

## Definition of done

- ☐ All 13 `UseInMemoryDatabase` call sites removed from `src/` (12 mechanical + Example rewrite).
- ☐ All 14 `Microsoft.EntityFrameworkCore.InMemory` package references removed from `src/**/*.csproj`.
- ☐ Example module uses config.json + dual-provider + provider-aware migrations + SQL Server
      migration set + `ExampleDbContextSqlServerDesignTimeFactory`.
- ☐ `dotnet build DotNetCloud.CI.slnf -c Release` passes.
- ☐ `dotnet test tests/DotNetCloud.Modules.Example.Tests` passes.
- ☐ `rg "UseInMemoryDatabase" src/` returns nothing.
- ☐ `Directory.Packages.props` still pins `Microsoft.EntityFrameworkCore.InMemory` 10.0.10 (tests need it).

## Risks / notes

- **Standalone dev convenience is lost:** running a module host with no DB config now throws
  instead of booting with a throwaway DB. This is intentional (fail-fast). Developers must supply
  `config.json` or `ConnectionStrings:DefaultConnection`.
- **Do not kill/abort a long-running `dotnet build`.** Wait for it to complete.
- **Do not reformat unrelated code** — keep diffs minimal and targeted so review is easy.
- If `dotnet ef` cannot generate the SQL Server migration for Example due to the two design-time
  factories, temporarily remove the PostgreSQL factory, generate, then restore it (see 2e).

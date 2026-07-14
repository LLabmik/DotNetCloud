# Plan: Promote Notes to Required Module (+ Fix Contacts Inconsistency)

> **Feature Branch:** `feature/notes-required-module` (already created from `main`)
>
> **Created:** 2026-07-14
>
> **Status:** Ready for implementation

---

## Summary

Add `dotnetcloud.notes` to the architecturally required modules list. This means Notes cannot be disabled or uninstalled at runtime, and its database tables move from the `notes` schema to the `core` schema.

**Side fix:** Remove `dotnetcloud.contacts` from the setup wizard's optional modules array. Contacts is already in `RequiredModules.ModuleIds` but was incorrectly left in the optional array — a pre-existing inconsistency.

---

## Impact Analysis

### Schema Change

| Aspect            | Before                                        | After                                       |
| ----------------- | --------------------------------------------- | ------------------------------------------- |
| Schema            | `notes`                                       | `core`                                      |
| PostgreSQL tables | `notes.notes`, `notes.note_folders`, …        | `core.notes`, `core.note_folders`, …        |
| SQL Server tables | `[notes].[Notes]`, `[notes].[NoteFolders]`, … | `[core].[Notes]`, `[core].[NoteFolders]`, … |

### Table Collision Check: ✅ CLEAN

No Notes entity names collide with existing core schema entities:

| Notes Entity  | Collision? |
| ------------- | ---------- |
| `Note`        | No         |
| `NoteFolder`  | No         |
| `NoteTag`     | No         |
| `NoteLink`    | No         |
| `NoteVersion` | No         |
| `NoteShare`   | No         |

### Existing CoreDbContext DbSets (for reference)

Organizations, Teams, TeamMembers, Groups, GroupMembers, OrganizationMembers, Permissions, Roles, RolePermissions, SystemSettings, OrganizationSettings, UserSettings, UserDevices, Notifications, InstalledModules, ModuleCapabilityGrants, OpenIddictApplications, OpenIddictAuthorizations, OpenIddictTokens, OpenIddictScopes, UserBackupCodes, FidoCredentials, Users, Roles, UserClaims, UserLogins, UserTokens, RoleClaims

### Files That Auto-Adapt (No Changes Needed)

These files use `RequiredModules.IsRequired()` or `RequiredModules.GetSchemaName()` dynamically, so they automatically pick up the change:

- `src/Core/DotNetCloud.Core.Data/Naming/PostgreSqlNamingStrategy.cs` — delegates to `GetSchemaName()`
- `src/Core/DotNetCloud.Core.Data/Naming/SqlServerNamingStrategy.cs` — delegates to `GetSchemaName()`
- `src/Core/DotNetCloud.Core.Schema/Services/DbContextSchemaProvider.cs` — drop protection uses `IsRequired()`
- `src/Core/DotNetCloud.Core.Server/Initialization/ModuleUiRegistrationHostedService.cs` — seeds `IsRequired` dynamically
- `src/Core/DotNetCloud.Core.Server/Services/AdminModuleService.cs` — stop guard uses `module.IsRequired`
- `src/CLI/DotNetCloud.CLI/Commands/ModuleCommands.cs` — stop/uninstall guard uses `IsRequired()`
- `src/Modules/Notes/DotNetCloud.Modules.Notes.Data/NotesDbContext.cs` — schema resolved via `_namingStrategy.GetSchemaForModule("notes")` which delegates to `RequiredModules.GetSchemaName()`
- `src/Modules/Notes/DotNetCloud.Modules.Notes.Host/Program.cs` — no optional/required logic at all

---

## Step-by-Step Implementation

### Step 1: Add `"dotnetcloud.notes"` to `RequiredModules.ModuleIds`

**File:** `src\Core\DotNetCloud.Core\Modules\RequiredModules.cs`
**Lines:** 13-18 (the `ModuleIds` HashSet initializer)

**Old code (lines 12-19):**

```csharp
    public static readonly IReadOnlySet<string> ModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dotnetcloud.files",
        "dotnetcloud.chat",
        "dotnetcloud.search",
        "dotnetcloud.contacts",
        "dotnetcloud.calendar",
        "dotnetcloud.about"
    };
```

**New code:**

```csharp
    public static readonly IReadOnlySet<string> ModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dotnetcloud.files",
        "dotnetcloud.chat",
        "dotnetcloud.search",
        "dotnetcloud.contacts",
        "dotnetcloud.calendar",
        "dotnetcloud.notes",
        "dotnetcloud.about"
    };
```

Also update the XML doc comment summary on line 4 to say "7" instead of "6" or just remove the number:

```csharp
/// Required modules: files, chat, search, contacts, calendar, notes, about.
```

---

### Step 2: Fix SetupCommand.cs — Remove Contacts and Notes from optional list

**File:** `src\CLI\DotNetCloud.CLI\Commands\SetupCommand.cs`

#### Step 2a: Update the optionalModules array

**Lines 475-480:**

```csharp
        var optionalModules = new[]
        {
            "dotnetcloud.contacts",
            "dotnetcloud.notes",
            "dotnetcloud.tracks"
        };
```

**New code:**

```csharp
        var optionalModules = new[]
        {
            "dotnetcloud.tracks"
        };
```

#### Step 2b: Update the beginner mode message

**Lines 493-495:**

```csharp
            ConsoleOutput.WriteInfo("Keeping the first install simple: only the required modules are enabled.");
            ConsoleOutput.WriteInfo("You can enable Contacts, Notes, and Tracks later from the admin UI.");
```

**New code:**

```csharp
            ConsoleOutput.WriteInfo("Keeping the first install simple: only the required modules are enabled.");
            ConsoleOutput.WriteInfo("You can enable Tracks later from the admin UI.");
```

#### Step 2c: Update the required modules comment

**Line 473:**

```csharp
        // Required modules (Files, Chat, Search, Calendar, About) are always enabled.
```

**New code:**

```csharp
        // Required modules (Files, Chat, Search, Contacts, Calendar, Notes, About) are always enabled.
```

#### Step 2d: Update the interactive module selection prompt

Since `optionalModules` now only has `"dotnetcloud.tracks"`, the loop at lines 497-509 will only prompt for Tracks. No code change needed — it works correctly. But if there's a message like "Select optional modules to enable" at line 503, keep it as-is.

---

### Step 3: Regenerate EF Core Migrations for Notes Module

After Step 1 changes `RequiredModules.ModuleIds`, the `GetSchemaName("notes")` will return `"core"` instead of `"notes"`. The existing EF Core model snapshot expects tables in the `notes` schema. A new migration must be generated.

**Command to run:**

```powershell
dotnet ef migrations add PromoteNotesToRequiredModule `
    --project src\Modules\Notes\DotNetCloud.Modules.Notes.Data `
    --context NotesDbContext `
    --startup-project src\Core\DotNetCloud.Core.Server
```

⚠️ **Important:** The `--startup-project` is `DotNetCloud.Core.Server` because the Notes module's Host project uses in-memory database. The core server project has the full EF configuration needed for migration generation.

**Expected result:** The generated migration should contain ALTER TABLE statements moving all 6 tables from the `notes` schema to the `core` schema, plus updates to the model snapshot.

**If the migration generator fails** (e.g., because `NotesDbContext` isn't registered in `Core.Server`), an alternative is to:

1. Temporarily add `NotesDbContext` registration to `Core.Server/Program.cs`:
   ```csharp
   // Temporary for migration generation
   services.AddDbContext<NotesDbContext>(options =>
       options.UseNpgsql(connectionString));
   ```
2. Generate the migration
3. Revert the temporary registration
4. Commit only the migration files

**Existing migrations in the Notes module:**

- `src\Modules\Notes\DotNetCloud.Modules.Notes.Data\Migrations\20260323145420_InitialCreate.cs`
- `src\Modules\Notes\DotNetCloud.Modules.Notes.Data\Migrations\20260501221720_FixPendingModelChanges.cs`
- `src\Modules\Notes\DotNetCloud.Modules.Notes.Data\Migrations\NotesDbContextModelSnapshot.cs`

---

### Step 4: Update Documentation

#### Step 4a: `docs\MASTER_PROJECT_PLAN.md`

**Location 1 — Line 3821:** (req-modules-schema-1 notes)
**Old:**

```
**Notes:** Phase 1 complete. `RequiredModules.ModuleIds` defines `dotnetcloud.files`, `dotnetcloud.chat`, `dotnetcloud.search` as architecturally required.
```

**New:**

```
**Notes:** Phase 1 complete. `RequiredModules.ModuleIds` defines `dotnetcloud.files`, `dotnetcloud.chat`, `dotnetcloud.search`, `dotnetcloud.contacts`, `dotnetcloud.calendar`, `dotnetcloud.notes`, `dotnetcloud.about` as architecturally required.
```

**Location 2 — Line 5964:** (duplicate notes — same fix)
**Old:**

```
**Notes:** Phase 1 complete. `RequiredModules.ModuleIds` defines `dotnetcloud.files`, `dotnetcloud.chat`, `dotnetcloud.search` as architecturally required.
```

**New:**

```
**Notes:** Phase 1 complete. `RequiredModules.ModuleIds` defines `dotnetcloud.files`, `dotnetcloud.chat`, `dotnetcloud.search`, `dotnetcloud.contacts`, `dotnetcloud.calendar`, `dotnetcloud.notes`, `dotnetcloud.about` as architecturally required.
```

#### Step 4b: `docs\REQUIRED_MODULES_AND_SCHEMA_SEPARATION_PLAN.md`

**Line 1 area (summary paragraph near top):**
Find text like "three architecturally required modules: files, chat, search" and update to reflect the actual list (files, chat, search, contacts, calendar, notes, about).

**Design section 1 (lines ~90-100, the `RequiredModules.cs` code block):**
Update the code block to match the current `ModuleIds`:

```csharp
public static readonly IReadOnlySet<string> ModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "dotnetcloud.files",
    "dotnetcloud.chat",
    "dotnetcloud.search",
    "dotnetcloud.contacts",
    "dotnetcloud.calendar",
    "dotnetcloud.notes",
    "dotnetcloud.about"
};
```

**Phase 1.1 section (lines ~145-155, the code block for `RequiredModules.cs`):**
Same update as above.

#### Step 4c: `docs\IMPLEMENTATION_CHECKLIST.md`

Search for any checklist items related to "required modules" or "Notes optional" and update if needed. This is a lower priority — only update if there are specific items about Notes being optional.

---

### Step 5: Build and Verify

#### 5a: Build

```powershell
dotnet build
```

Expected: 0 errors.

If the migration generation in Step 3 required temporary changes, revert those before the final build.

#### 5b: Run Tests

```powershell
dotnet test
```

Focus especially on:

- Notes module tests: `dotnet test tests\DotNetCloud.Modules.Notes.Tests\`
- Core tests: `dotnet test tests\DotNetCloud.Core.Tests\`
- Core Data tests: `dotnet test tests\DotNetCloud.Core.Data.Tests\`

Expected: All tests pass with 0 failures.

#### 5c: Manual Verification (Optional but Recommended)

After tests pass, verify the following programmatically or by inspection:

1. `RequiredModules.IsRequired("dotnetcloud.notes")` returns `true`
2. `RequiredModules.IsRequired("notes")` returns `true` (short form)
3. `RequiredModules.GetSchemaName("dotnetcloud.notes")` returns `"core"`
4. `RequiredModules.IsRequired("dotnetcloud.contacts")` returns `true` (should already)
5. Setup wizard beginner message no longer mentions "Contacts" or "Notes" as enable-later

---

## Order of Operations

```
1. Step 1: Update RequiredModules.cs          ← Must be first
2. Step 2: Update SetupCommand.cs              ← Can be parallel with Step 1
3. Step 3: Regenerate EF migrations            ← Depends on Step 1 (schema name changes)
4. Step 4: Update documentation                ← Can be parallel with Step 3
5. Step 5: Build, test, and verify             ← Depends on Steps 1-3
```

---

## Files Changed Summary

| File                                                           | Change                                                    |
| -------------------------------------------------------------- | --------------------------------------------------------- |
| `src\Core\DotNetCloud.Core\Modules\RequiredModules.cs`         | Add `"dotnetcloud.notes"` to `ModuleIds`                  |
| `src\CLI\DotNetCloud.CLI\Commands\SetupCommand.cs`             | Remove contacts & notes from optional array; fix messages |
| `src\Modules\Notes\DotNetCloud.Modules.Notes.Data\Migrations\` | New migration + updated snapshot                          |
| `docs\MASTER_PROJECT_PLAN.md`                                  | Update 2 locations with correct required module list      |
| `docs\REQUIRED_MODULES_AND_SCHEMA_SEPARATION_PLAN.md`          | Update code blocks and text to reflect actual list        |
| `docs\IMPLEMENTATION_CHECKLIST.md`                             | Update if relevant (optional)                             |

---

## Commit Message Suggestion

```
feat: promote Notes to required module, fix Contacts setup inconsistency

- Add dotnetcloud.notes to RequiredModules.ModuleIds
- Remove dotnetcloud.contacts from SetupCommand optional array (already required)
- Remove dotnetcloud.notes from SetupCommand optional array (now required)
- Update beginner mode message to reflect only Tracks as optional
- Regenerate Notes EF migration for schema change (notes → core)
- Update documentation to reflect current required module list
```

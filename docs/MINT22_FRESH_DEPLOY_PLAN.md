# Mint22 Fresh Deploy Plan (via `deploy.sh`)

**Status:** In progress — `scripts/deploy.sh` changes implemented (2026-08-19)
**Date:** 2026-08-19
**Target machine:** `mint22` (dev server, `https://mint22:5443/`)

## 1. Purpose

Bring the `mint22` install — currently many versions behind `main` — up to the
current source tree with a single full rebuild and redeploy, while guaranteeing
that **all pending EF Core migrations are applied** before the service starts.

## 2. Scope Decisions (confirmed)

- **In-place full rebuild.** Replace all binaries from the current source
  checkout; preserve `config.json`, data directories, the PostgreSQL database,
  and the existing systemd unit.
- **Refresh the CLI too.** `deploy.sh` deliberately excludes the CLI project,
  but the `mint22` CLI at `/opt/dotnetcloud/dotnetcloud` is also many versions
  behind and must be rebuilt alongside the server.
- **Migrations are mandatory.** The deploy aborts (does not start the service)
  if the migration step fails, with a newbie-level explanation and recovery steps.
- Future `install.sh` / release fixes (v0.4.04) are **out of scope** for this
  deploy.

## 3. Why `deploy.sh` Instead of `install.sh`

`mint22` already has a correct installation skeleton:

| Component    | Expected path                                                  |
| ------------ | -------------------------------------------------------------- |
| CLI apphost  | `/opt/dotnetcloud/dotnetcloud`                                 |
| Core server  | `/opt/dotnetcloud/server/DotNetCloud.Core.Server`              |
| Module hosts | `/opt/dotnetcloud/server/modules/dotnetcloud.<name>/`          |
| systemd unit | `Type=forking`, `ExecStart=/opt/dotnetcloud/dotnetcloud start` |

This skeleton matches the release-tarball layout and
`SystemdServiceHelper.GenerateUnitFile`. The `dotnetcloud start` path bridges
`config.json` values into `Kestrel__*` / `ConnectionStrings__*` environment
variables, so the server binds the correct ports and TLS certificate.

`install.sh` has drifted from this pattern (direct-DLL `Type=simple` unit that
skips that env bridging, a CLI DLL path bug, and a missing Notes host in
`release.yml`). Those are `install.sh` regressions — not a problem with the old
`mint22` deploy — and will be addressed separately after this deploy validates
the current source.

### 3.1 Actual state discovered on `mint22` (2026-08-19)

Pre-flight inspection found the machine is a mix of older layouts, which is why
recent `deploy.sh` runs did not update the running service:

- `/opt/dotnetcloud/dotnetcloud` is a **symlink** to `/opt/dotnetcloud/cli/dotnetcloud`.
- The live service runs `/opt/dotnetcloud/cli/server/DotNetCloud.Core.Server`
  (May 26) with an **empty** `cli/server/modules/` — no module hosts are running.
- `deploy.sh` publishes to `/opt/dotnetcloud/server/` (Aug 15), which the service
  never loads.
- `/opt/dotnetcloud/` root also contains a stale flat dump (Apr 6) of the server,
  module hosts, and CLI all in one directory.

Consequence: deploys since ~May 26 wrote to a directory the service ignores —
which matches the "many versions behind" symptom. The fix is a one-time cleanup
that converges the machine onto the canonical layout above, followed by a normal
`deploy.sh --force`.

## 4. Migration Flow (verified in code)

- **Core + required modules** (`files`, `chat`, `search`, `contacts`, `calendar`,
  `notes`, `about` — all share the `core` schema): applied by
  `CoreDbContext.Database.MigrateAsync()` via `DbInitializer` at server startup,
  and by `dotnetcloud migrate` (`RunMigrateOnlyAsync`).
- **Optional modules** (`tracks`, `photos`, `music`, `video`, `ai`, `bookmarks`,
  `email`): applied by `ModuleSchemaService` → `DbContextSchemaProvider`
  (`MigrateAsync` / `CreateMissingTables`), executed by the **server process** at
  startup and by `dotnetcloud migrate`. Module hosts do not migrate themselves.
- Therefore, running `dotnetcloud migrate` **after publishing the new binaries
  and before starting the service** is the deterministic way to apply everything
  and surface errors early.

## 5. Changes to `scripts/deploy.sh`

Two targeted edits:

### 5.1 Refresh the CLI (root layout)

In the full-build path, after the Core.Server publish:

- Publish `src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj` (framework-dependent,
  `AssemblyName` = `dotnetcloud`) to a staging directory.
- Copy `dotnetcloud`, `dotnetcloud.dll`, and the dependency DLLs to
  `/opt/dotnetcloud/` (root), with the same stale-DLL guard already used for the
  core server.
- This also refreshes the root copy of `DotNetCloud.Core.Data.dll` and the module
  `*.Data.dll` assemblies, so `dotnetcloud migrate` sees the **new** migration
  classes.

### 5.2 Add a migration phase before service start

Inserted between "Fix permissions" and "Start service":

1. Run `sudo /opt/dotnetcloud/dotnetcloud migrate`.
2. Capture full stdout + stderr to `/var/log/dotnetcloud/deploy-migrate.log` and
   echo it to the terminal (never suppress stderr).
3. Capture the exit code (survive `set -e`) so a friendly block can be printed.
4. On success: continue to start the service.
5. On failure: print the newbie-level failure block below and `exit 1` — **do
   not start the service**.

### 5.3 Newbie-level migration-failure message

When `dotnetcloud migrate` exits non-zero, print (in plain language):

```text
════════════════════════════════════════════════════════════════
  DotNetCloud was updated, but the database upgrade did not finish.
════════════════════════════════════════════════════════════════

What happened:
  • The new DotNetCloud program files were copied onto this machine.
  • The step that updates the database to match them did not complete.
  • DotNetCloud was left STOPPED so it won't start with a half-updated database.

Good news:
  • No data was lost or deleted. Your existing data is exactly as it was before.

Right now:
  • New program files are installed on disk.
  • The database is still on its previous version.
  • The DotNetCloud service is stopped.

The database error was:
  <last error line(s) from the migrate output>
  Full log: /var/log/dotnetcloud/deploy-migrate.log

Common causes and fixes:
  • The database is not running
      Start it:  sudo systemctl start postgresql
  • The database connection details are wrong
      Fix them:  sudo dotnetcloud setup
  • The database user does not have permission
      See the "Database Permissions Notice" shown during setup
  • The disk is full
      Check:     df -h

To try again:
  1. Fix the cause above.
  2. Run:  sudo dotnetcloud migrate
     (repeat until it finishes without errors)
  3. Then: sudo ./scripts/deploy.sh --force

  If you're stuck, open an issue:
      https://github.com/LLabmik/DotNetCloud/issues
════════════════════════════════════════════════════════════════
```

## 6. Deploy Steps

### Phase 1 — Prep on `mint22`

1. `git pull` on `main`; confirm a clean working tree and note the commit hash.
2. Confirm the skeleton is intact:
   - `systemctl cat dotnetcloud.service`
   - `ls -l /opt/dotnetcloud/dotnetcloud /opt/dotnetcloud/server/DotNetCloud.Core.Server`
   - `id dotnetcloud`
3. Snapshot the database (safety):
   `sudo -u postgres pg_dump dotnetcloud > /root/dotnetcloud-predeploy-$(date +%Y%m%d).sql`
4. One-time layout cleanup (removes only old binaries; config/data/db/unit are
   outside `/opt/dotnetcloud` and are untouched):

   ```bash
   sudo find /opt/dotnetcloud -maxdepth 1 -mindepth 1 \
     ! -name server ! -name VERSION \
     -exec rm -rf {} +
   ```

### Phase 2 — Modify `scripts/deploy.sh`

4. ✓ Add the CLI refresh (section 5.1).
5. ✓ Add the migration phase with the newbie-level failure block (sections 5.2 and 5.3).

### Phase 3 — Deploy

6. `sudo ./scripts/deploy.sh --force --verify`
   (full rebuild of Core.Server + all 14 module hosts, CLI refresh, explicit
   migrations, then service start).

### Phase 4 — Config schema review

7. If `config.json` has `configSchemaVersion` less than `2` (old install), run
   `sudo dotnetcloud setup` once to review new options (previous values are
   pre-filled) and confirm `enabledModules` contains all required + desired
   optional modules before the migrate step.

## 7. Verification Checklist

- ☐ `sudo /opt/dotnetcloud/dotnetcloud status` → Server Running, HTTP 5080, HTTPS 5443
- ☐ `systemctl status dotnetcloud` → active
- ☐ `sudo journalctl -u dotnetcloud -n 50` → no migration errors
- ☐ `curl -kfsS https://localhost:5443/health/live` → `Healthy`
- ☐ `curl -fsS http://localhost:5080/health/live` → `Healthy`
- ☐ Re-run `sudo /opt/dotnetcloud/dotnetcloud migrate` → reports "up to date"
- ☐ All 14 module hosts healthy (`dotnetcloud status` / process list / journal)
- ☐ Log in at `https://mint22:5443` and smoke-test Files, Notes, Chat, Calendar

## 8. Out of Scope (separate future work)

- `install.sh` fixes: CLI DLL path, missing Notes host in `release.yml`,
  first-boot `env` file ordering, and the Kestrel unit regression.
- The `v0.4.04` release (created only after this deploy proves current source is stable).
- `uninstall.sh` database-drop prompt (still desired later; not needed for an
  in-place redeploy).

## 9. Relevant Files

- `scripts/deploy.sh` — add CLI refresh + explicit migrate phase
- `src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj` — CLI publish target
- `src/CLI/DotNetCloud.CLI/Commands/SetupCommand.cs` — `RunMigrateOnlyAsync`
- `src/Core/DotNetCloud.Core.Data/Initialization/DbInitializer.cs` — server auto-migration
- `src/Core/DotNetCloud.Core.Server/Services/DbContextSchemaProvider.cs` — module schema migrations
- `src/Core/DotNetCloud.Core/Modules/RequiredModules.cs` — required module set

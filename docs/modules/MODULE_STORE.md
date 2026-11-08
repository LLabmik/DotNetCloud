# Module store and package installer (B3)

> **Audience:** server operators and administrators installing modules from a `.dncpkg` package.
> Module authors and store operators start with [`MODULE_PACKAGING.md`](MODULE_PACKAGING.md).

## 1. What the first release does

The first release of the installer (phase B3, decision D6) installs from:

* **a file** — `dotnetcloud module install --file ./org.example.receipts-1.0.0.dncpkg`, or an upload
  through the admin UI;
* **a local feed directory** — a directory holding an `index.json`, the signed package files and an
  optional `yanked.json` (`dotnetcloud module install --from-feed /srv/store-feed org.example.receipts`).

There is **no network access** in this path: no remote index, no mirrors, no automatic update check.
`IModuleStoreService` is the seam a future release implements for those; the verification and install
code below does not change when it arrives.

## 2. The install state machine

Every entry point (CLI and admin API) runs the same sequence. Nothing is written until step 5, and a
failure after step 6 is undone.

| # | Step | What happens on failure |
|---|------|-------------------------|
| 1 | **Verify** | Archive safety, required entries, manifest (core loader), compatibility, SHA-256, Ed25519 signature. Refused → `ModulePackageVerificationFailed` is audited and nothing changes. |
| 2 | **Review** | The manifest facts are returned to the caller: publisher, hash, key id, capabilities, consent scopes, events. Nothing changes. |
| 3 | **Acknowledge** | The caller must echo the reviewed hash **and** the exact capability and scope lists. A package that changed, or that declares anything the reviewer did not see, is refused. |
| 4 | **Provision** | An isolated data store (schema + principal) is created *before* any binary moves. A provisioning failure leaves the previous install untouched. |
| 5 | **Stage** | The package is extracted into `modules/.staging/<id>/<guid>/`, the host runtime's binaries are selected and flattened to the install root, and an `install-receipt.json` is written. |
| 6 | **Swap** | The live `modules/<id>` moves to `modules/.previous/<id>/<previous-version>`; the staged tree is promoted. On failure the previous tree is restored. |
| 7 | **Record** | `InstalledModules` is upserted: `TrustTier = Store`, publisher, signing key id, data-store mode, `Enabled` for a first install and `RestartPending` for an upgrade. |
| 8 | **Start + health** | The server starts the module and waits up to `Modules:Store:HealthCheckTimeout` (default 30 s). A module that never reports healthy is **rolled back**: the previous binaries are restored, the record is restored to the previous version and the module is left disabled. A first install that fails is removed entirely. |

Installing through the **CLI** stops after step 7: module processes belong to the running server, so
the CLI records the install and the supervisor's 30-second reconcile starts or restarts the module.
Health-check rollback therefore only happens for installs performed by the server itself (admin API).

### Rollback

The version replaced by an install is kept, so `dotnetcloud module rollback <id>` (or the admin API)
can restore it. A rollback refuses a version the store has **withdrawn** (`yanked.json`), because
restoring a revoked build is exactly the failure mode F16 describes.

A rollback swaps binaries only. Modules must ship **expand/contract** database migrations so that the
older binaries keep working against the newer schema; the installer never drops or rewinds a module's
data store.

### Unverified installs (sideload)

A package is only installed without an explicit override when its detached signature verifies against
a key id in `Modules:Store:TrustedKeys`. Otherwise:

* the CLI refuses unless `--allow-unverified` is passed (it prints the package SHA-256 first);
* the admin UI refuses unless the "not verified by a key this server trusts" box is ticked;
* the server refuses unless `Modules:Store:AllowUnverified` is `true`;
* the install is recorded with **no** `SignedByKeyId`, the receipt says `"unverified": true`, and
  `ModulePackageVerificationFailed` is audited with the hash.

## 3. Configuration

`config.json` (or environment variables using `__` as the separator):

```json
{
  "Modules": {
    "Store": {
      "LocalFeedPath": "/srv/dotnetcloud/store-feed",
      "AllowUnverified": false,
      "HealthCheckTimeout": "00:00:30",
      "KeepPreviousVersion": true,
      "TrustedKeys": {
        "store-2026": "<base64 32-byte Ed25519 public key>"
      },
      "RevokedKeyIds": []
    }
  }
}
```

| Key | Default | Meaning |
|-----|---------|---------|
| `LocalFeedPath` | `store-feed` (sibling of the modules directory) | Feed directory used by `--from-feed` without an argument, `module verify` and the admin catalogue. |
| `AllowUnverified` | `false` | Whether this server **permits** unverified installs at all. Even when true, each install must opt in explicitly. |
| `TrustedKeys` | `{}` | Key id → base64 public key. Keys are only trusted from configuration, never from a feed. |
| `RevokedKeyIds` | `[]` | Key ids that must never verify again (rotation after compromise). |
| `HealthCheckTimeout` | 30 s | How long a fresh install has to become healthy before it is rolled back. |
| `KeepPreviousVersion` | `true` | Retain the replaced version for `module rollback`. |

The trusted key list is the whole trust root: a feed is data, so keys carried by a feed are ignored.

## 4. Layout on disk

```
<modules>/
├── org.example.receipts/                     live install (discovered by the supervisor)
│   ├── org.example.receipts.dll              binaries of the selected runtime
│   ├── manifest.json
│   ├── LICENSE / README.md / CHANGELOG.md
│   └── install-receipt.json                  package hash, key id, unverified flag
├── .previous/org.example.receipts/1.0.0/     retained version, for rollback
└── .staging/org.example.receipts/<guid>/     in-flight extraction
```

Both working directories start with a dot and are skipped by module discovery, so a half-extracted
tree can never be registered as an installed module.

## 5. Command reference

```
dotnetcloud module install <module-id>                 # register a module already extracted on disk
dotnetcloud module install --file <package.dncpkg> [--allow-unverified] [--yes]
dotnetcloud module install --from-feed <dir> <module-id> [--version <ver>] [--allow-unverified] [--yes]
dotnetcloud module verify <package.dncpkg>
dotnetcloud module info <module-id>
dotnetcloud module rollback <module-id>
dotnetcloud module list | start <id> | stop <id> | restart <id> | uninstall <id> [--destroy-data] [--keep-files]
```

Admin API (all `RequireAdmin`):

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `api/v1/core/admin/modules/store` | List the configured feed's packages with the installed version. |
| `POST` | `api/v1/core/admin/modules/install/inspect` | Upload a package; returns the review. Installs nothing. |
| `POST` | `api/v1/core/admin/modules/install` | Multipart upload plus `expectedSha256`, repeated `acknowledgedCapabilities` / `acknowledgedScopes`, `allowUnverified`, `startAfterInstall`. |
| `POST` | `api/v1/core/admin/modules/{id}/rollback` | Restore the retained version. |

Admin UI: **Module Store** (`/admin/store`) — upload, review the declared capabilities and scopes,
approve, install.

## 6. Why these decisions

* **The review is two steps.** A single "install this file" call would make the administrator's
  approval apply to whatever bytes arrive; echoing the hash and the declared lists binds the approval
  to exactly what was shown.
* **The data store is provisioned before the swap.** Otherwise a failed provisioning would leave the
  new binaries on disk with no schema, or the old binaries with a half-created one.
* **A failed first install is removed entirely.** Leaving a `Failed` row and a directory behind would
  make the module appear installed and broken rather than absent.
* **Discovery skips dot-directories.** The installer needs working space inside the modules root, and
  the alternative (a temp directory on another filesystem) would make the promote step a copy instead
  of an atomic rename.
* **Manifest rules are not duplicated.** The installer runs the core's own `ModuleManifestLoader`, so
  a package cannot be installable but unstartable.

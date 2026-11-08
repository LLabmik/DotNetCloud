# Module store and package installer (B3)

> **Audience:** server operators and administrators installing modules from a `.dncpkg` package.
> Module authors and store operators start with [`MODULE_PACKAGING.md`](MODULE_PACKAGING.md).

## 1. What the installer does

The installer accepts packages from three sources:

- **a file** — `dotnetcloud module install --file ./org.example.receipts-1.0.0.dncpkg`, or an upload
  through the admin UI;
- **a local feed directory** — a directory holding an `index.json`, the signed package files and an
  optional `yanked.json` (`dotnetcloud module install --from-feed /srv/store-feed org.example.receipts`);
- **a remote signed index** — a published store (for example
  `https://modules.dotnetcloud.net/index/v1/index.json`) configured with `Modules:Store:IndexUrl`
  (`dotnetcloud module install --from-store org.example.receipts`).

Setting `IndexUrl` replaces the local feed rather than adding to it, so exactly one source is ever
active. All three sources funnel into the same verification and install path below: the remote client
only adds *acquisition* (fetch, cache, download), never trust of its own.

### The remote index

- The index is fetched over HTTPS together with its detached signature `<IndexUrl>.sig`. The
  signature must verify against one of `Modules:Store:TrustedKeys`; an index signed by any other key
  — or whose bytes do not match the signed SHA-256 — is rejected outright and nothing is cached.
- The verified index is cached under `Modules:Store:CachePath` for `Modules:Store:IndexCacheTtl`
  (default one hour), so restarts do not refetch. A failed refresh keeps serving the cached copy and
  logs a warning; it never breaks the admin store page.
- An index whose `generatedAt` is **older** than the cached one is refused (replay protection). An
  operator who deliberately wants to undo a publish sets `Modules:Store:AllowIndexRollback`.
- A package is downloaded only when its `<id>/<version>` directory has no file with the published
  size and SHA-256. The download is size-capped, hashed after arrival, and compared with the signed
  index entry before it is moved into place.
- The signature file the installer verifies is written from the signed index itself, so a package is
  never installed on the strength of a signature that arrived separately from the catalogue.
- A relative `downloadUrl` resolves against `Modules:Store:DownloadBaseUrl` (default: the index
  origin), because published catalogues use site-root-relative paths such as
  `packages/<id>/<version>/<file>.dncpkg`. An absolute `downloadUrl` must be `https` **and** on the
  index's own origin or an origin listed in `Modules:Store:DownloadOrigins` — a compromised index
  cannot redirect installs to a third-party host.
- Index entries are validated before use: schema version 1, semver versions, 64-hex SHA-256, sane
  sizes, no duplicate `<id>@<version>`, and ids/versions that are safe path segments.

## 2. The install state machine

Every entry point (CLI and admin API) runs the same sequence. Nothing is written until step 5, and a
failure after step 6 is undone.

| #   | Step               | What happens on failure                                                                                                                                                                                                                                                                                                     |
| --- | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Verify**         | Archive safety, required entries, manifest (core loader), compatibility, SHA-256, Ed25519 signature. Refused → `ModulePackageVerificationFailed` is audited and nothing changes.                                                                                                                                            |
| 2   | **Review**         | The manifest facts are returned to the caller: publisher, hash, key id, capabilities, consent scopes, events. Nothing changes.                                                                                                                                                                                              |
| 3   | **Acknowledge**    | The caller must echo the reviewed hash **and** the exact capability and scope lists. A package that changed, or that declares anything the reviewer did not see, is refused.                                                                                                                                                |
| 4   | **Provision**      | An isolated data store (schema + principal) is created _before_ any binary moves. A provisioning failure leaves the previous install untouched.                                                                                                                                                                             |
| 5   | **Stage**          | The package is extracted into `modules/.staging/<id>/<guid>/`, the host runtime's binaries are selected and flattened to the install root, and an `install-receipt.json` is written.                                                                                                                                        |
| 6   | **Swap**           | The live `modules/<id>` moves to `modules/.previous/<id>/<previous-version>`; the staged tree is promoted. On failure the previous tree is restored.                                                                                                                                                                        |
| 7   | **Record**         | `InstalledModules` is upserted: `TrustTier = Store`, publisher, signing key id, data-store mode, `Enabled` for a first install and `RestartPending` for an upgrade.                                                                                                                                                         |
| 8   | **Start + health** | The server starts the module and waits up to `Modules:Store:HealthCheckTimeout` (default 30 s). A module that never reports healthy is **rolled back**: the previous binaries are restored, the record is restored to the previous version and the module is left disabled. A first install that fails is removed entirely. |

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

- the CLI refuses unless `--allow-unverified` is passed (it prints the package SHA-256 first);
- the admin UI refuses unless the "not verified by a key this server trusts" box is ticked;
- the server refuses unless `Modules:Store:AllowUnverified` is `true`;
- the install is recorded with **no** `SignedByKeyId`, the receipt says `"unverified": true`, and
  `ModulePackageVerificationFailed` is audited with the hash.

### Trust tiers

Every catalogue entry carries the tier the store **derived** for it, and the client shows it: the
admin store list has a **Trust** column, the review panel repeats it, and the CLI prints it before
asking for confirmation.

| Tier                 | Meaning                                                                                  |
| -------------------- | ---------------------------------------------------------------------------------------- |
| `official`           | Signed by the DotNetCloud project's publisher key — the project itself publishes it       |
| `verified-publisher` | Registered publisher whose identity the store has verified                                 |
| `community`          | Registered publisher, published after review, identity not verified by the store          |
| `unverified`         | Not signed by a key this server pins — sideload only, with an explicit override           |

`unverified` is never published in a catalogue; this server derives it from its own signature check.
A file or local-feed package that passes the key check is `community`, because there is no store
curation behind it to claim more. A catalogue value this client does not recognise is shown as
`Unspecified` rather than guessed at.

The tier is **informational** and deliberately distinct from `InstalledModule.TrustTier`
(`Required` / `Bundled` / `Store`): only the latter decides whether capabilities are auto-granted. A
community-tier package still needs explicit admin approval for every capability it declares.

The store recomputes the tier on every validation, so a hand-edited catalogue cannot promote itself;
the store-side rules are in `DotNetCloud.ModuleStore/policy/STORE_POLICY.md` §2.

### Revocation warnings for installed versions

Yanking never stops a running module, so the server has to tell an administrator when a version it
**already runs** has changed status. The admin store page (`/admin/store`) and `dotnetcloud module
list` compare every store-tier install with the configured catalogue and report:

| Severity       | Meaning                                                                        |
| -------------- | ------------------------------------------------------------------------------ |
| `security`     | Withdrawn for a vulnerability or malicious behaviour — upgrade or uninstall     |
| `deprecation`  | Withdrawn as broken or superseded — it keeps working, but no update will come   |
| `unlisted`     | The catalogue no longer lists the installed version at all (a takedown)         |

Each warning names the installed version, the store's reason and the newest **non-withdrawn**
version, which is what the admin store page's **Review upgrade** action fetches. The severity is
published in the catalogue (`yankedSeverity`) so the client never has to guess it, and
the endpoint (`GET api/v1/core/admin/modules/store/revocations`) returns nothing when the store cannot
be reached — "cannot check" is deliberately not reported as "all clear".

Bundle modules are never checked: only installs whose provenance is `Store` come from the catalogue.

## 3. Configuration

`config.json` (or environment variables using `__` as the separator):

```json
{
  "Modules": {
    "Store": {
      "IndexUrl": "https://modules.dotnetcloud.net/index/v1/index.json",
      "Mirrors": [],
      "CachePath": "store-cache",
      "IndexCacheTtl": "01:00:00",
      "DownloadTimeout": "00:01:00",
      "DownloadBaseUrl": null,
      "DownloadOrigins": [],
      "AllowIndexRollback": false,
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

| Key                   | Default                                         | Meaning                                                                                                          |
| --------------------- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `IndexUrl`            | _(unset)_                                       | Published store index. When set it **replaces** the local feed; must be `https` (loopback `http` for dev).        |
| `Mirrors`             | `[]`                                            | Additional index URLs tried in order when `IndexUrl` cannot be fetched or verified.                              |
| `CachePath`           | `store-cache` (beside the application)          | Where the verified index and downloaded packages are cached.                                                     |
| `IndexCacheTtl`       | 1 hour                                          | How long a cached index is served before it is refetched.                                                        |
| `DownloadTimeout`     | 60 s                                            | Per-request timeout for index and package downloads.                                                             |
| `DownloadBaseUrl`     | the index origin                                | Base for relative `downloadUrl` values (`https://modules.dotnetcloud.net/`).                                    |
| `DownloadOrigins`     | `[]`                                            | Extra origins an absolute `downloadUrl` may use. Same-origin is always allowed.                                  |
| `AllowIndexRollback`  | `false`                                         | Accept an index older than the cached one (deliberate store rollback only).                                       |
| `LocalFeedPath`       | `store-feed` (sibling of the modules directory) | Feed directory used by `--from-feed` without an argument, `module verify` and the admin catalogue.               |
| `AllowUnverified`     | `false`                                         | Whether this server **permits** unverified installs at all. Even when true, each install must opt in explicitly. |
| `TrustedKeys`         | `{}`                                            | Key id → base64 public key. Keys are only trusted from configuration, never from a feed or an index.             |
| `RevokedKeyIds`       | `[]`                                            | Key ids that must never verify again (rotation after compromise).                                                |
| `HealthCheckTimeout`  | 30 s                                            | How long a fresh install has to become healthy before it is rolled back.                                         |
| `KeepPreviousVersion` | `true`                                          | Retain the replaced version for `module rollback`.                                                               |

The trusted key list is the whole trust root: a feed and an index are data, so keys they carry are
ignored. Pin both the store index key and every publisher key you intend to accept.

### Key rotation

Pin the **current and previous** index key ids while the store rotates:

```json
"TrustedKeys": {
  "store-2027": "<base64 current>",
  "store-2026": "<base64 previous>"
}
```

An index signed by either verifies, so servers do not have to move in lock-step with the store.
`RevokedKeyIds` wins over `TrustedKeys`, so a compromised key can be disabled without deleting its
entry. Drop the previous id once the overlap release has shipped. The store-side procedure (key
generation, secret storage, offline backup, rotation, compromise response) is
`DotNetCloud.ModuleStore/docs/KEY_MANAGEMENT.md`.

### Prerequisites on the host

- **PostgreSQL `CREATEROLE`.** Installing a package whose manifest declares `dataStore: "isolated"`
  provisions a per-module principal, which needs the app role to hold `CREATEROLE`:
  `ALTER ROLE "<app-role>" CREATEROLE;` as a superuser, then restart the server. Without it the
  install stops with `DATASTORE_PROVISIONING_FAILED` and the remediation text naming the real role.
  Installs created by the current setup wizard already grant it; older ones do not.
- **`CachePath` must be writable by the service account.** Under the systemd unit the application
  directory is read-only (`ProtectSystem=strict`), so set it under the data directory, for example
  `/var/lib/dotnetcloud/store-cache`. The client creates the directory on first use.
- **Packages ship portable RIDs** (`files/linux-x64`, `files/win-x64`). The installer matches a
  distro host such as `ubuntu.24.04-x64` by OS family plus architecture, so one `linux-x64` build
  serves every glibc distribution; Alpine/musl hosts need `linux-musl-<arch>`. An exact
  `files/<host-rid>` directory still wins when a package ships one, and a package that only carries
  a foreign OS is refused with `PACKAGE_NO_MATCHING_RUNTIME`.

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

The remote client keeps its own state beside the application (or `Modules:Store:CachePath`):

```
<store-cache>/
├── index/
│   ├── index.json          verified index (rewritten atomically on refresh)
│   └── state.json          fetchedAt / generatedAt / source URL
└── packages/<id>/<version>/
    ├── <id>-<version>.dncpkg      downloaded, size- and SHA-256-checked
    └── <id>-<version>.dncpkg.sig  written from the signed index
```

A `.part` file never survives an attempt: downloads are staged, verified, then moved into place.

## 5. Command reference

```
dotnetcloud module install <module-id>                 # register a module already extracted on disk
dotnetcloud module install --file <package.dncpkg> [--allow-unverified] [--yes]
dotnetcloud module install --from-feed <dir> <module-id> [--version <ver>] [--allow-unverified] [--yes]
dotnetcloud module install --from-store <module-id> [--version <ver>] [--allow-unverified] [--yes]
dotnetcloud module verify <package.dncpkg>
dotnetcloud module info <module-id>
dotnetcloud module rollback <module-id>
dotnetcloud module list | start <id> | stop <id> | restart <id> | uninstall <id> [--destroy-data] [--keep-files]
```

`--from-store` uses whichever source is configured: the remote index when `Modules:Store:IndexUrl` is
set, otherwise the local feed. It resolves the package, downloads and verifies it, then runs the same
review and install flow as `--file`.

Admin API (all `RequireAdmin`):

| Method | Route                                                  | Purpose                                                                                                                                     |
| ------ | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET`  | `api/v1/core/admin/modules/store`                      | List the configured source's packages with the installed version.                                                                            |
| `POST` | `api/v1/core/admin/modules/store/{id}/inspect?version=` | Fetch the listed package and return the review. Installs nothing.                                                                          |
| `POST` | `api/v1/core/admin/modules/store/{id}/install`         | JSON body: `version`, `expectedSha256`, `acknowledgedCapabilities` / `acknowledgedScopes`, `allowUnverified`, `startAfterInstall`.           |
| `POST` | `api/v1/core/admin/modules/install/inspect`            | Upload a package; returns the review. Installs nothing.                                                                                     |
| `POST` | `api/v1/core/admin/modules/install`                    | Multipart upload plus `expectedSha256`, repeated `acknowledgedCapabilities` / `acknowledgedScopes`, `allowUnverified`, `startAfterInstall`. |
| `POST` | `api/v1/core/admin/modules/{id}/rollback`              | Restore the retained version.                                                                                                               |

Admin UI: **Module Store** (`/admin/store`) — the configured catalogue is listed with a **Review**
action per version, which fetches and verifies the package and shows the same approval step as an
upload. Uploading a `.dncpkg` still works for air-gapped installs.

## 6. Why these decisions

- **The review is two steps.** A single "install this file" call would make the administrator's
  approval apply to whatever bytes arrive; echoing the hash and the declared lists binds the approval
  to exactly what was shown.
- **The data store is provisioned before the swap.** Otherwise a failed provisioning would leave the
  new binaries on disk with no schema, or the old binaries with a half-created one.
- **A failed first install is removed entirely.** Leaving a `Failed` row and a directory behind would
  make the module appear installed and broken rather than absent.
- **Discovery skips dot-directories.** The installer needs working space inside the modules root, and
  the alternative (a temp directory on another filesystem) would make the promote step a copy instead
  of an atomic rename.
- **Manifest rules are not duplicated.** The installer runs the core's own `ModuleManifestLoader`, so
  a package cannot be installable but unstartable.
- **The index signature and the package signature are separate checks.** The index signature proves
  the catalogue came from the store; the package signature proves the publisher produced those bytes.
  Requiring both means a compromised index still cannot inject a package, and a compromised publisher
  key still cannot be installed unless the store published it.
- **An older index is refused by default.** Without the `generatedAt` monotonicity check, anyone who
  can replay an old signed index (a stale mirror, a cached CDN object, an on-path attacker) could roll
  the catalogue back to a version the store has already superseded.
- **Download URLs are origin-restricted.** A signed index is authoritative about *what* to install,
  not about *where to fetch it from*; letting it point at any host would turn the store into an SSRF
  primitive and a cross-origin downgrade path.

# Module Security Model

How a module is allowed to reach user data, and what a user gets to say about it.

This document is the reference for the **authorization plane** of the module sandbox
(phase A3 of `docs/THIRD_PARTY_MODULE_SANDBOX_AND_STORE_PLAN.md`). It defines the scope catalog,
the capability → scope mapping, the enforcement rules, the consent flow, and the account-erasure
contract.

---

## 1. Two independent decisions

A module call that touches personal data needs **two** separate approvals:

| Decision         | Decided by    | Stored as               | Answers                               |
| ---------------- | ------------- | ----------------------- | ------------------------------------- |
| Capability grant | Administrator | `ModuleCapabilityGrant` | May this module call this API at all? |
| User consent     | Each end user | `ModuleUserConsent`     | May this module touch **my** data?    |

Neither implies the other:

- The admin installing a "Receipt Scanner" module grants `IStorageProvider` because the module
  needs the storage API. That must not silently mean "the publisher may read every user's files".
- A user approving `files:read` for that module does not bypass the capability grant: without the
  admin grant the call is already denied with `CAPABILITY_NOT_GRANTED` before consent is consulted.

System capabilities — notifications, the event bus, settings, audit, search submit — carry no
personal data of their own and need **no** consent. Note that `INotificationService` still maps to
`notifications:send`, because "this module may notify me" is a user preference; the capability grant
remains the gate on the code path.

---

## 2. Scope catalog

`DotNetCloud.Core.Modules.ModuleScopes` is the single source of truth. The OpenIddict scope
registry, the first-party client seeder, module OIDC client registration, manifest validation, the
consent prompt, the "Connected apps" page and token-introspection filtering all read it.

| Scope                | Grants                                             | Consent required      |
| -------------------- | -------------------------------------------------- | --------------------- |
| `profile`            | Read your name, email address and profile picture. | Yes                   |
| `files:read`         | Read your files and folders.                       | Yes                   |
| `files:write`        | Create, change and delete your files and folders.  | Yes                   |
| `contacts:read`      | Read your contacts.                                | Yes                   |
| `contacts:write`     | Create, change and delete your contacts.           | Yes                   |
| `calendar:read`      | Read your calendars and events.                    | Yes                   |
| `calendar:write`     | Create, change and delete your calendar events.    | Yes                   |
| `notes:read`         | Read your notes.                                   | Yes                   |
| `notes:write`        | Create, change and delete your notes.              | Yes                   |
| `mail:read`          | Read your email.                                   | Yes                   |
| `mail:write`         | Send, change and delete your email.                | Yes                   |
| `chat:read`          | Read your chat conversations.                      | Yes                   |
| `chat:write`         | Send messages in your chat conversations.          | Yes                   |
| `tracks:read`        | Read your Tracks boards and work items.            | Yes                   |
| `tracks:write`       | Create and change your Tracks work items.          | Yes                   |
| `photos:read`        | Read your photos.                                  | Yes                   |
| `photos:write`       | Upload, change and delete your photos.             | Yes                   |
| `music:read`         | Read your music library.                           | Yes                   |
| `music:write`        | Change your music library and playlists.           | Yes                   |
| `video:read`         | Read your videos.                                  | Yes                   |
| `video:write`        | Upload, change and delete your videos.             | Yes                   |
| `bookmarks:read`     | Read your bookmarks.                               | Yes                   |
| `bookmarks:write`    | Create, change and delete your bookmarks.          | Yes                   |
| `notifications:send` | Send notifications to you.                         | Yes (delivery filter) |

`openid`, `email` and `offline_access` are standard OIDC scopes, not consent records. `profile` is
both: it is the OIDC profile scope and the consent record that unlocks an email address.

Adding a scope means adding it to `ModuleScopes.ConsentScopes` **once**. A manifest that declares a
scope the catalog does not know is rejected (`MODULE_INVALID_MANIFEST`), because a scope the core
cannot describe is a scope a user cannot meaningfully approve.

---

## 3. Capability → scope mapping

`ModuleCapabilityScopes` answers "if a module asks for capability X, which user data can it reach?".

| Capability             | Required consent scope(s) |
| ---------------------- | ------------------------- |
| `IUserDirectory`       | `profile`                 |
| `ICurrentUserContext`  | `profile`                 |
| `IStorageProvider`     | `files:read`              |
| `INotificationService` | `notifications:send`      |

**Validation (F18).** Once a v2 manifest declares at least one `userConsentScopes` entry, every
user-data capability it requests must be covered by a declared scope, or the manifest is rejected.
A module that declares no scopes at all is unaffected — see the transitional rule below.

---

## 4. Enforcement rules

### 4.1 Hard denial — `MODULE_CONSENT_REQUIRED`

Returned when the whole call exists to read one user's data and the user has not approved it:

| RPC                | Required scope |
| ------------------ | -------------- |
| `GetTeamsForUser`  | `profile`      |
| `GetGroupsForUser` | `profile`      |

The denial is `PermissionDenied` with:

```
error-code:      MODULE_CONSENT_REQUIRED
consent-scopes:  profile
```

The REST facade reports the same information as HTTP 403 with
`details.consentScopes = ["profile"]`, so a REST caller can launch the consent flow for exactly the
missing scopes.

### 4.2 Withholding — personal data inside a wider result

Some calls must succeed but must not leak personal data:

| RPC                | Rule                                                                                                                                                                                                                  |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SearchUsers`      | An email address is returned only for users who consented to `profile` for this module. Names and avatars stay available — a member list is unusable without them, and the capability grant already gates the search. |
| `GetCurrentUser`   | Email is never returned.                                                                                                                                                                                              |
| `IntrospectToken`  | Scopes are narrowed to the module's declared scopes ∩ the user's consent; the `email` claim is dropped without `profile` consent.                                                                                     |
| `SendNotification` | When the module declares `notifications:send`, recipients without consent are filtered out.                                                                                                                           |

### 4.3 Transitional rule

A module whose manifest declares **no** `userConsentScopes` is treated as not consent-aware: the
capability grant is the only gate, exactly as before A3. This is what keeps required and bundled
modules (which declare none today) working until Track C migrates them. A store module cannot
exploit it: F18 validation at manifest load requires the declaration as soon as the module opts in
to one scope, and the store review checks the declaration against what the module actually does.

---

## 5. Consent flow

```
module client ──┐
                │  GET /connect/authorize?client_id=dnc-module-…&scope=files:read
                ▼
        core: consent already granted?  ── yes ──► sign in (authorization code)
                │ no
                ▼
        GET /connect/consent?rid=<unguessable id>   (core chrome, frame-ancestors 'none')
                │
                │ POST /connect/consent  { rid, decision=allow|deny, __RequestVerificationToken }
                ▼
        allow ──► ModuleUserConsent rows written ──► redirect to /connect/authorize?…&dnc_consent=<rid>
        deny  ──► access_denied to the client
```

**Design rules:**

1. **The prompt is core chrome.** It is never rendered inside a module iframe, so a module cannot
   draw, restyle, pre-check or clickjack its own consent prompt.
2. **POST-only decisions, anti-forgery protected.** A GET renders the prompt and can never approve
   or deny. A POST without a valid anti-forgery token is rejected with 400 and writes nothing — the
   auto-approve and clickjack cases.
3. **The form carries only a prompt id.** Client id, scopes and the redirect target come from the
   server-side `ConsentRequestStore` entry, bound to the signed-in user. A tampered form cannot
   widen an approval to scopes the user never saw.
4. **Single use.** The prompt id is consumed exactly once by the authorize endpoint, expires after
   10 minutes, and is rejected if presented by a different user.
5. **Responses are non-frameable.** `Content-Security-Policy: frame-ancestors 'none'`,
   `X-Frame-Options: DENY`, `default-src 'none'; style-src 'unsafe-inline'; form-action 'self'`,
   `Cache-Control: no-store`.
6. **First-party clients never prompt** (decision D3): the desktop, mobile and browser-extension
   clients are registered with `ConsentTypes.Implicit`. Only `dnc-module-*` clients are `Explicit`.
7. **Module clients are public PKCE clients** with no client secret. Shipping a secret into a
   module process would turn a consent record into a credential that can mint tokens without a user
   present.

**Re-prompting.** Consent rows survive module updates. A new version that adds a scope simply has no
row for it, so the user is prompted the first time the module needs that scope and keeps everything
they already approved.

**Revocation.** Revocation deletes the row and invalidates the (30-second) grant cache before the
change is committed, so the next call is already denied. Revocation stops future calls; it does not
take data back from a module that already has it — store review and the audit log are the
compensating controls.

**Where users see and revoke consent.** `/connected-apps` (linked from the profile page) lists each
module with plain-language descriptions of the approved scopes, and an administrator can see the
per-scope consent counts and revoke on a user's behalf from the admin module detail page. Both
actions are audited (`ModuleConsentGranted`, `ModuleConsentRevoked`, `ModuleConsentDenied`).

---

## 6. Shared and team data (decision)

Consent is evaluated for **the user the call acts for** — the subject of the verified
`dnc-user-assertion`, not the owner of the underlying object.

- A module acting for Alice over a folder Bob shared with her is checked against
  `(module, Alice, files:read)`. Bob's consent is irrelevant to Alice's read, and vice versa.
- A module acting on its own behalf (no assertion — a background sweep) has no consent subject. It
  must not exist for consent-aware modules: user-scoped data is only reachable through a
  user-scoped call, which is exactly what subject binding (F4) enforces.
- An administrator's shared-folder grant is an **organizational** authorization. It still does not
  let a module read a user's private data, and it does not substitute for that user's consent.
- Therefore a module that processes a team artifact reads it as the requesting member. If the
  artifact contains another member's personal fields, that member's consent is required before the
  data is returned — the module must degrade gracefully (`CheckUserConsent`) rather than assume it.

This model is the precondition for the Track C media migrations: no media module may assume
"consent of the uploader" covers a shared album.

---

## 7. Module-facing consent queries

```proto
rpc CheckUserConsent (CheckUserConsentRequest) returns (CheckUserConsentResponse);
rpc GetGrantedScopes (GetGrantedScopesRequest) returns (GetGrantedScopesResponse);
```

Both accept an empty `user_id`, meaning "the acting user from the assertion", and are subject-bound:
naming a different user than the verified caller is rejected with `AUTH_SUBJECT_MISMATCH`. The
answers are stored consent **intersected with the scopes the module declared**, so a module can
never be told it holds a scope its own manifest does not claim.

They are also exposed on the REST facade as `GET /internal/moduleapi/v1/consent/scopes` and
`GET /internal/moduleapi/v1/consent/{userId}` .

---

## 7.1 The user's files (IFileDirectory / IFileWriter)

A module that needs the user's real files — attach a file to an email, save an attachment back into
the drive — reaches them through the core, never through another module:

```proto
rpc ListFiles (ListFilesRequest) returns (ListFilesResponse);        // IFileDirectory, files:read
rpc DownloadFile (DownloadFileRequest) returns (stream FileContentChunk); // IFileDirectory, files:read
rpc UploadFile (stream UploadFileRequest) returns (UploadFileResponse);   // IFileWriter, files:write
```

REST facade: `GET /internal/moduleapi/v1/files?parentId=`,
`GET /internal/moduleapi/v1/files/{nodeId}/content` and
`POST /internal/moduleapi/v1/files?parentId=&fileName=&contentType=` (the request body is the content).

**Why it is brokered rather than direct.** A module may not dial another module's socket, read its
schema or hold its credential (§4.1 of the sandbox plan). The core calls the Files module over the
same peer-authenticated gRPC channel it already uses, attaching a **core-signed assertion for the
acting user** — so the Files host sees a normal authenticated user and its own permission checks
still apply (including shared-folder and quota rules). The module never names a user in the payload.

**Three gates, in order.**

1. **Transport** — the module credential proves which process is calling.
2. **Capability** — `ListFiles`/`DownloadFile` map to `IFileDirectory`, `UploadFile` to
   `IFileWriter`; both are **Restricted**, so the install needs an explicit grant, and a module that
   only exports can be granted read-only access.
3. **Consent** — `(module, user, files:read)` for reads and `(module, user, files:write)` for writes.
   A missing consent fails with `MODULE_CONSENT_REQUIRED` (REST: `403` + `details.consentScopes`),
   which is what the module UI turns into a consent prompt.

There is **no module-scope form**: a call without a verified end-user assertion is answered
`UNAUTHENTICATED` rather than guessed at, because "whose files" has no safe default.

**Bounds.** One save may carry at most 100 MiB; a listing is capped at 500 entries per page; the
file name is reduced to a single sanitized path segment by the core (a module cannot write outside
the user's tree by naming `../../…`). Reading and writing the user's own tree is all the capability
can express — rename, move and delete stay with the Files UI.

**Boundary statement.** The capability is only as strong as the Files module's own authorization:
the core asserts _who_ is asking, and the Files module decides _what that user may touch_. A module
granted the capability therefore sees exactly what the user sees, and nothing more.

---

## 8. Account erasure (F12)

Deleting an account must delete the user's data everywhere, including module schemas and
module-owned files.

**Contract (core → module).** `dotnetcloud.lifecycle.ModuleLifecycle/EraseUserData`:

```proto
rpc EraseUserData (EraseUserDataRequest) returns (EraseUserDataResponse);
```

| Field            | Meaning                                                                               |
| ---------------- | ------------------------------------------------------------------------------------- |
| `request_id`     | Stable id of the erasure request. Repeated calls with the same id must be idempotent. |
| `user_id`        | The deleted user.                                                                     |
| `scopes`         | Advisory: the consent scopes the module held.                                         |
| `acknowledged`   | True when everything the module holds for the user is gone.                           |
| `deleted_counts` | Per-scope rows/objects deleted, for operator visibility.                              |
| `error`          | Why the erasure could not complete.                                                   |
| `not_applicable` | True when the module holds no per-user data.                                          |

**Implementation.** Every module host derives from `TrackedModuleLifecycleBase`, which implements the
RPC. A host that stores per-user data registers an `IModuleUserErasureHandler`:

```csharp
builder.Services.AddModuleUserErasureHandler<FilesUserErasureHandler>();
```

A host that registers nothing answers `not_applicable` — "I hold nothing" and "I could not erase"
must never look the same to the orchestrator.

**Orchestration.**

1. `UserDeletedEvent` (published after the identity row is gone) creates a `UserErasureRequest`.
2. Consent rows for the user are deleted immediately — they are personal data and must not wait on
   an offline module.
3. One `ModuleUserErasure` row is created per installed module whose manifest declares
   `handlesUserErasure: true`.
4. Delivery runs immediately and then every 5 minutes for **72 hours**.
5. The request is `Completed` only when every module row is `Acknowledged`. Otherwise it stays
   `Pending` for 72 hours and then becomes `Failed`.
6. An administrator can force-complete a failed request (`POST /api/v1/core/admin/erasure/{id}/override`),
   which is audited as `ModuleUserErasureOverridden`. The admin queue is
   `GET /api/v1/core/admin/erasure`.

Backups retain the data until their retention expiry; that is documented in the store policy.
Aggregated or anonymised data is only acceptable when declared and consented.

---

## 9. Core↔module transport

Every core↔module connection is cleartext HTTP/2 (h2c) with no TLS, because both ends are local
processes owned by the same service. Identity is carried by the per-launch **module credential**
(`dnc-module-token`), never by the transport.

### 9.1 Transport matrix

| Transport          | Endpoint string                                       | Where it applies                                                                       | Artifact / permission                                              |
| ------------------ | ----------------------------------------------------- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Unix domain socket | `unix://<dataDir>/modules/<moduleId>/run/module.sock` | Linux, macOS — **default**                                                             | Runtime directory `0700`, socket file `0600`, owned by the service |
| Windows named pipe | `net.pipe://dotnetcloud-<moduleId>`                   | Windows — **default**                                                                  | `PipeOptions.CurrentUserOnly` (DACL = service account only)        |
| Loopback TCP       | `http://localhost:<port>`                             | `Modules:Transport=Tcp`, legacy `PreferTcpTransport=true`, or an over-long socket path | Port from `ProcessSupervisor:TcpPortRange` (50101–50500)           |

### 9.2 Configuration

```json
{
  "Modules": {
    "Transport": "Auto",
    "UnixSocketDirectory": null,
    "NamedPipePrefix": "dotnetcloud"
  }
}
```

- `Transport` — `Auto` (default), `Sockets`, `Tcp`. Resolved once at startup by
  `ModuleTransportSelection`; every consumer (supervisor, gRPC channel manager, REST proxy,
  endpoint provider) reads the same resolved value.
- `UnixSocketDirectory` — optional override. Set this when the data directory path is too deep for
  a `sockaddr_un` (≈107 bytes); a shared directory uses `<moduleId>.sock` filenames.
- `ProcessSupervisor:PreferTcpTransport` — legacy opt-in retained for compatibility. It only applies
  when `Modules:Transport` is `Auto`, and it now defaults to `false` (it used to be forced `true`,
  which made sockets unusable).

### 9.3 What the transport does and does not protect

**Does:**

- Removes the loopback listening port from every module, so a module is not reachable from other
  network interfaces on the host, and only the core knows where to dial.
- Restricts socket/pipe access to the service account (other OS users are excluded).
- Keeps the credential check (`ModulePeerAuthenticationInterceptor` host-side,
  `AuthenticationInterceptor` core-side) on every call regardless of transport — a test asserts that
  a caller who can open a module's socket is still rejected without that module's credential.

**Does not:**

- Separate modules from each other. `0600`/`CurrentUserOnly` key on the **OS user**, and module
  hosts run as the core's user, so a sibling module can connect to another module's socket. The
  credential is what stops it from being _accepted_. Eliminating this requires per-module OS
  accounts (a public-store launch gate) or connection-level core→module authentication.
- Encrypt traffic. Nothing leaves the host; TLS would add cost without adding a boundary.

### 9.4 Deployment guidance

- **Bare metal / single container:** the default (`Auto`) works. The socket lives under
  `<DOTNETCLOUD_DATA_DIR>/modules/<moduleId>/run/`.
- **Separate module containers:** mount the module data directory into every container so the
  socket path is visible in both, or set `Modules__Transport=Tcp`. A module started without the
  supervisor's credential fails closed either way.
- **Anything that cannot share a filesystem** (some PaaS runtimes): `Modules__Transport=Tcp`.
  TCP widens _reachability_, not _authorization_ — the credential check is unchanged.

### 9.5 Operational notes

- The supervisor removes a module's socket file before every spawn and after every stop or crash,
  because a hard-killed host leaves the file behind and the next bind fails with "address already
  in use". Windows pipes are released by the OS.
- A module whose computed socket path exceeds ~107 bytes falls back to loopback TCP with a warning
  logged by the supervisor; set `Modules:UnixSocketDirectory` to a short path instead.
- The core's own module-facing gRPC listener stays on loopback TCP (`TcpPortRangeStart`, default
  50100). Modules dial it with `DOTNETCLOUD_CORE_ENDPOINT`.

---

## 10. Module UI plane (`/apps/{moduleId}/**`)

A non-required module may serve its own web UI from its own process. The core reverse-proxies it at
`/apps/{moduleId}/**` and frames it; the shell page that hosts the frame lives at
`/modules/{moduleId}` so the user keeps the core chrome.

### 10.1 What the proxy guarantees

| Control                                             | Enforced by                                                                                                                                                     |
| --------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Only signed-in users                                | 401 from the proxy handler (never a login redirect — this endpoint is loaded by an iframe)                                                                      |
| Only modules that declared `ui.type = "iframe"`     | 404 otherwise, so the core origin never becomes a passthrough to an arbitrary module HTTP surface                                                               |
| Module must be running                              | 503                                                                                                                                                             |
| Per-module request budget                           | `ModuleRateLimitKind.Ui` (separate counter from capability calls) → 429 + `Retry-After`                                                                         |
| The browser's core session never reaches the module | `Cookie`, `Authorization`, `dnc-user-assertion`, `dnc-module-token` and `dnc-module-id` are stripped before forwarding                                          |
| The module cannot set cookies on the core origin    | `Set-Cookie` stripped from every proxied response                                                                                                               |
| The module cannot relax framing                     | The module's `Content-Security-Policy`/`X-Frame-Options` are **replaced** with `frame-ancestors 'self'; base-uri 'none'; object-src 'none'; form-action 'self'` |
| Response hygiene                                    | `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store` for HTML documents                                                 |
| Bounded responses                                   | 32 MiB body cap; a larger response is aborted as a proxy error                                                                                                  |
| Route safety                                        | The `dncmoduleid` constraint only matches reverse-DNS ids, so `/apps/files` and friends stay with the shell                                                     |

The user identity reaches the module exactly as it does for the REST API plane: a core-signed
`dnc-user-assertion` whose audience is that module, valid for 60 seconds, with the operation set to
`ui`. A module cannot mint one and cannot replay one against another module.

### 10.2 The iframe itself

`ModuleAppHost` frames the module with `sandbox="allow-scripts allow-forms allow-popups allow-modals"`
— deliberately **without** `allow-same-origin`. The document therefore has an opaque origin:

- It cannot read core cookies, `localStorage`, the DOM, or core storage.
- It cannot read the response of anything it fetches from the core. It **can** read its own module's
  API responses: those calls are cross-origin (opaque origin), so the proxy answers the preflight and
  returns `Access-Control-Allow-Origin: null` with `Access-Control-Allow-Credentials: true`. That is
  safe because the proxy authenticates the call and mints the assertion; a third-party page's request
  to the same path is cross-site, so the SameSite cookie is not attached and it gets 401.
- `allow-modals` permits `alert`/`confirm`/`prompt` and `<dialog>.showModal()`. It grants no access to
  storage, clipboard, geolocation or the core origin.
- Every message it sends reports `event.origin === "null"`, so **origin checks are useless**. Both
  sides authenticate the peer by identity: the host requires `event.source === iframe.contentWindow`
  for that specific frame, and `dncModuleBridge.js` requires `event.source === window.parent`. Message
  types are additionally restricted per direction, the payload is capped at 8 KiB, and unknown types
  are dropped.
- `dnc:notify` is rate-limited to 5 per minute (bridge _and_ core), `dnc:openExternal` accepts
  `https:` only and opens with `noopener,noreferrer`, and `dnc:navigate` accepts module-relative paths
  only.

### 10.3 CSRF position (F2)

The sandbox is not a network boundary: an opaque-origin document can still _send_ requests to the core
origin, it just cannot read the responses. Two controls apply:

1. The core's session cookie is `SameSite=Lax`, `HttpOnly`, `Secure`. Requests from an opaque-origin
   frame are cross-site, so the cookie is not attached to them at all.
2. The proxy strips the cookie before forwarding, so a module backend never holds a core session even
   when the request carried one.

What is **not** covered: a module's own server-side code, once it holds a scoped assertion, can call
the core API as that user for the scopes it has consented to. That is the consent model, not a UI
hole. Moving the plane to a dedicated `apps.<host>` origin would add browser-level origin separation
on top; it remains a public-store launch gate (§13 of the sandbox plan).

### 10.4 Consent from a framed UI

`dnc:requestConsent` does **not** start an OIDC flow yet. The core renders a banner listing the
requested scopes in plain language and links the user to `/connected-apps`, where the actual grant
lives. The OIDC-popup upgrade needs module-side PKCE clients and a redirect-URI contract, and is
tracked with the store client (Track B).

---

## 11. Lifecycle operations (uninstall, credentials, audit)

### 11.1 Uninstall and data retention

Uninstalling is an operator action (`POST /api/v1/core/admin/modules/{id}/uninstall`,
`dotnetcloud module uninstall`) and runs in a fixed order:

1. Stop the module process.
2. Revoke its launch credential, so a lingering process can no longer call the core.
3. Drop the isolated data store **or** retain it as `Orphaned`.
4. Delete the installation row; capability grants, quotas, settings and consent records cascade.
5. Remove the module binaries (path-checked against the modules root; `--keep-files` skips it).

**A failed drop aborts the uninstall.** The installation row stays, so the operator can stop the
module and retry instead of being left with an unmanaged schema and no record of it.

Retaining data marks `ModuleDataStore.Status = Orphaned` and clears the encrypted connection string
(the schema and principal stay, still owned by the module principal). Orphaned stores are listed by
`GET /api/v1/core/admin/datastores/orphaned` and `dotnetcloud datastore status`, and are dropped with
`dotnetcloud datastore destroy <id>`. There is deliberately **no foreign key** from
`ModuleDataStore` to `InstalledModule`: a cascade would delete the row that records the retained data.

### 11.2 Launch credentials

A module credential is a 32-byte secret minted by the supervisor on every launch and required on
every capability call (A2). It is never persisted and never leaves the core:

- `GET modules/{id}/credential` returns only `Active`, a four-character hint and the
  issue/rotate/revoke timestamps.
- **Rotate** marks the rotation and restarts the module, which mints a new secret; the old value stops
  working immediately.
- **Revoke** invalidates the current secret. The running process is rejected on every call until it is
  restarted (which mints a fresh credential).

A core restart re-issues fresh credentials to every module it spawns, so revocation survives only as
long as the process it targeted.

### 11.3 Audit integrity (F14)

Every audit field that originates from a module is untrusted. `AuditLogService` runs it through
`AuditTextSanitizer`, which removes C0/C1 control characters (including CR/LF, so a module cannot
forge a log record) and caps `ModuleId` at 200, `EntityType` at 100 and `Description` at 1000
characters.

Module audit writes are rate-limited per minute (`AuditEntriesPerMinute`). The **first** rejection in
each window is audited as `ModuleRateLimited`; subsequent rejections in the same window are not, so a
runaway module cannot amplify load through the audit path.

Operator actions are audited with dedicated actions (`ModuleInstalled`, `ModuleUpdated`,
`ModuleUninstalled`, `ModuleCredentialRotated`, `ModuleDataStoreProvisioned|Destroyed|Error`,
`ModuleLegacyAccessUsed`). The idempotent startup registry reconciliation is not audited: it runs on
every boot and would bury operator actions in noise.

### 11.4 Health and supervision

`/health/ready` reports one row per supervised module, including `sandboxStatus`, `trustTier` and
`dataStoreMode`, so a module that is not running can be explained without opening the admin UI. A
module that fails `ProcessSupervisor:UnhealthyThreshold` consecutive probes is force-killed and handed
to its restart policy rather than lingering in `Degraded`.

### 11.5 Backup and restore

Sandboxed module data lives in `<dataDir>/modules/<id>/data` and the connection strings are encrypted
with the key ring in `<dataDir>/data-protection-keys`. Both are inside the configured data directory,
so a full backup includes them. Restoring an archive without the key ring leaves the stored module
connection strings undecryptable; the recovery path is
`dotnetcloud datastore reset-credentials <id>`, which regenerates the principal password and
re-encrypts the connection string with the current key ring.

---

## 12. What this model does not cover

- **Scope enforcement inside a module.** Consent governs what the core hands to a module. A module
  that has `files:read` still decides what it does with the bytes; store review and the audit log are
  the controls.
- **Other OAuth clients.** An explicit-consent client that is not a module has no consent record to
  store (there is no `InstalledModule` row), so the authorize endpoint lets it through. Tracking
  non-module OAuth clients is out of scope for the store launch.
- **OS-level isolation.** Sockets are reachable by any process running as the core service account;
  the credential is the API-layer identity, not an OS boundary. See §7 and phase A4.

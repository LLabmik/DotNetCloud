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
`GET /internal/moduleapi/v1/consent/{userId}`.

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

| Transport             | Endpoint string                                            | Where it applies                                                                | Artifact / permission                                              |
| --------------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Unix domain socket    | `unix://<dataDir>/modules/<moduleId>/run/module.sock`      | Linux, macOS — **default**                                                      | Runtime directory `0700`, socket file `0600`, owned by the service |
| Windows named pipe    | `net.pipe://dotnetcloud-<moduleId>`                        | Windows — **default**                                                           | `PipeOptions.CurrentUserOnly` (DACL = service account only)         |
| Loopback TCP          | `http://localhost:<port>`                                  | `Modules:Transport=Tcp`, legacy `PreferTcpTransport=true`, or an over-long socket path | Port from `ProcessSupervisor:TcpPortRange` (50101–50500)            |

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
  credential is what stops it from being *accepted*. Eliminating this requires per-module OS
  accounts (a public-store launch gate) or connection-level core→module authentication.
- Encrypt traffic. Nothing leaves the host; TLS would add cost without adding a boundary.

### 9.4 Deployment guidance

- **Bare metal / single container:** the default (`Auto`) works. The socket lives under
  `<DOTNETCLOUD_DATA_DIR>/modules/<moduleId>/run/`.
- **Separate module containers:** mount the module data directory into every container so the
  socket path is visible in both, or set `Modules__Transport=Tcp`. A module started without the
  supervisor's credential fails closed either way.
- **Anything that cannot share a filesystem** (some PaaS runtimes): `Modules__Transport=Tcp`.
  TCP widens *reachability*, not *authorization* — the credential check is unchanged.

### 9.5 Operational notes

- The supervisor removes a module's socket file before every spawn and after every stop or crash,
  because a hard-killed host leaves the file behind and the next bind fails with "address already
  in use". Windows pipes are released by the OS.
- A module whose computed socket path exceeds ~107 bytes falls back to loopback TCP with a warning
  logged by the supervisor; set `Modules:UnixSocketDirectory` to a short path instead.
- The core's own module-facing gRPC listener stays on loopback TCP (`TcpPortRangeStart`, default
  50100). Modules dial it with `DOTNETCLOUD_CORE_ENDPOINT`.

---

## 10. What this model does not cover

- **Scope enforcement inside a module.** Consent governs what the core hands to a module. A module
  that has `files:read` still decides what it does with the bytes; store review and the audit log are
  the controls.
- **Other OAuth clients.** An explicit-consent client that is not a module has no consent record to
  store (there is no `InstalledModule` row), so the authorize endpoint lets it through. Tracking
  non-module OAuth clients is out of scope for the store launch.
- **OS-level isolation.** Sockets are reachable by any process running as the core service account;
  the credential is the API-layer identity, not an OS boundary. See §7 and phase A4.

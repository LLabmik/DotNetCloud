# Security Audit Remediation Plan

- **Source:** security audit run `DotNetCloud/run-1` (profile `standard`), source ref `fix/security-audit@0effc4bfca2a7786d2176529331399f504d215ab`.
- **Audit artifacts (outside the repository):** `C:\Users\benk\security-audit-skill\DotNetCloud\run-1\` — `REPORT.md`, `FINDINGS-DETAIL.md`, `NEEDS-VALIDATION.md`, `findings.json`, `coverage-ledger.json`.
- **Totals:** 45 records — 35 confirmed (0 critical, 14 high, 17 medium, 4 low), 9 needs-validation, 1 rejected.
- **Execution boundary:** source-only. No target code was built or run during the audit; runtime-dependent facts are in `NEEDS-VALIDATION.md`.
- **Status legend:** ✓ completed · ☐ pending (never `[x]`/`[ ]`).
- **Ownership:** server-side work is handed to mint22 through `docs/development/CLIENT_SERVER_MEDIATION_HANDOFF.md`; only client-local items are implemented on this Windows machine.

## Order of work

| Phase | Meaning | Workstreams | Findings |
| --- | --- | --- | --- |
| P0 | Structural and cheap-high. Fix now; blocks the rest. | 1, 2, 3 | 11 |
| P1 | Boundary sweeps. Next two weeks. | 2, 3, 4, 5, 6, 7, 8 | 24 |
| P2 | Validate-then-fix and hardening. After P0/P1. | 9 | 9 |

## Finding register

| Phase | Severity | Fingerprint | Owner | Regression test |
| --- | --- | --- | --- | --- |
| P0 | high | `authz.video.hls-segment.no-video-entitlement` | mint22 | Video test: user B requesting user A's HLS playlist and segment returns 404/403; a valid stream token for A still works. |
| P0 | high | `core-auth:account-state-transition-no-token-revocation` | mint22 | Integration test: disable a user, then reuse an access token and a refresh token issued before the change — both must fail; the cookie session must not renew. |
| P0 | high | `core-session:disabled-account-form-login` | mint22 | `AuthSessionController` test: POST the login form for an inactive user returns the failure view and sets no auth cookie. |
| P0 | high | `email.attachment.storage-key-path-traversal` | mint22 | Email host test: attachment key `../../appsettings.json` and an absolute path both return 404/400 and never read outside the attachment root. |
| P0 | high | `files-admin-shared-folder-group-grant-fail-open` | mint22 | Admin shared-folder test: an authenticated user with no matching grant does not see the mounted folder; a missing/empty group resolution denies by default. |
| P0 | high | `files-chunk-storage-quota-bypass` | mint22 | Quota test: completing chunks whose stored bytes exceed the user quota is rejected; usage is recomputed from stored bytes, not the declared TotalSize. |
| P0 | high | `files-chunked-upload-missing-target-write-authorization` | mint22 | `ChunkedUploadService` test: user B completing an upload into user A's folder returns PermissionDenied and leaves A's node unchanged. |
| P0 | high | `grpc/core-capabilities/self-asserted-module-id` | mint22 | gRPC integration test: a call with no module credential is rejected; a valid module credential limiter still cannot set `caller-user-id`/`caller-type` to anothe... |
| P0 | high | `grpc/module-hosts/request-user-id-without-peer-auth` | mint22 | Per-host gRPC test in the style of `FilesGrpcServiceSecurityTests`: a request whose JWT subject differs from `request.UserId` returns NotFound/PermissionDenied ... |
| P0 | medium | `core-session:login-form-csrf` | mint22 | A login/logout POST without a valid antiforgery token (or with a foreign Origin) is rejected; the happy path still signs in. |
| P0 | low | `secret-disclosure/tmdb-api-key-committed-installer` | mint22 | Rotate the key at the provider, remove it from `tools/install.sh`, and assert in CI that no provider key literal is present in the tree. |
| P1 | high | `access-control/core-hub/channel-group-membership-never-revoked` | mint22 | SignalR test: after leaving/being removed from a channel, no further channel events are delivered to that connection. |
| P1 | high | `access-control/core-hub/join-group-channel-membership-noop` | mint22 | SignalR test: joining another user's channel group does not deliver that channel's messages; the membership check returns false for a non-member. |
| P1 | high | `client-sync/tree-name-path-escape-outside-sync-root` | monolith | `Client.Core` test: server-supplied names `..\\..\\evil.txt`, `C:evil.txt`, `CON`, trailing-dot and long paths all resolve inside the sync root or are rejected. |
| P1 | high | `tracks-rest-workitem-object-idor-missing-product-membership` | mint22 | Tracks test: a non-member of a product gets 404/403 for work-item read, update, delete and attachment read. |
| P1 | high | `tracks/agile-services-missing-product-membership-predicate` | mint22 | Tracks test: non-member cannot read analytics/burndown, sprint contents, poker sessions, time entries or review sessions, and cannot delete/complete them. |
| P1 | medium | `access-control/chat-hub/delete-message-no-owner-check` | mint22 | Hub test: deleting another user's message without the same role rules as REST is rejected. |
| P1 | medium | `access-control/chat-hub/send-message-no-channel-membership` | mint22 | Hub test: sending to a channel the caller has not joined (or is blocked from) is rejected, matching the REST path. |
| P1 | medium | `ai-completion-queue-unbounded-fifo` | mint22 | Queue test: a per-user in-flight cap and a global depth cap reject rather than enqueue beyond the bound, with a clear error. |
| P1 | medium | `authz.video.screenshot.no-video-entitlement` | mint22 | Video test: the screenshot route resolves the caller and returns 404 for a video the caller does not own. |
| P1 | medium | `bookmarks-preview-endpoints-missing-owner-check` | mint22 | Bookmarks test: preview read/refresh for another user's bookmark returns 404 and does not rewrite its metadata. |
| P1 | medium | `chat-uploads-anonymous-read:no-channel-membership-binding:no-expiry-and-no-revocation` | mint22 | Chat test: an unauthenticated read of an upload URL whose channel the caller cannot see returns 404; a signed, expiring URL works only for its channel. |
| P1 | medium | `core-auth/claims-transformation/admin-perm-claim-cache-not-invalidated-on-role-removal` | mint22 | Authorization test: removing Administrator from a user invalidates the cached `dnc:perm=admin` claim on the next request (or within a documented, tested window)... |
| P1 | medium | `files/public-share/max-downloads-toctou@ShareService.cs:279+328` | mint22 | Concurrency test: N parallel downloads against a MaxDownloads=1 share result in exactly one success (conditional update or concurrency token). |
| P1 | medium | `oauth-revocation/module-introspection-accepts-revoked-access-token` | mint22 | Module test: after revoking an access token in the core, the module rejects it within the documented cache window (or immediately with token-entry validation). |
| P1 | medium | `oauth-scope/module-introspection-ignores-scope-and-audience` | mint22 | Module authorization test: a token minted with only `files:read` cannot call a write endpoint; an audience-restricted token is rejected by the wrong module. |
| P1 | medium | `photos-thumbnail-endpoint-missing-owner-or-share-check` | mint22 | Photos test: thumbnail of another user's photo and of a soft-deleted photo both return 404; a shared photo still returns the thumbnail. |
| P1 | medium | `search-index/calendar-creator-owner-acl-drift` | mint22 | Search test: after revoking a calendar share, a search by the former sharee no longer returns the event; a full reindex does not re-add it. |
| P1 | medium | `ssrf.safe-url-fetcher.dns-rebind-toctou` | mint22 | Fetcher test with a resolver returning a public address first and a private address on connect: the connection is pinned to the validated address and the second... |
| P1 | medium | `ssrf.tracks.webhook.user-url-not-validated` | mint22 | Webhook test: a URL resolving to 127.0.0.1/link-local/metadata is rejected at subscription and at delivery; a redirect to a private address is also rejected. |
| P1 | medium | `supply-chain/appimagetool-mutable-continuous-download-unsigned` | mint22 | Release script test: the tool download is pinned to an immutable version and verified against a recorded digest; a mismatch fails the job. |
| P1 | medium | `video-screenshot-subtitle-metadata-missing-user-video-ownership` | mint22 | Video test: screenshot, subtitle and metadata of another user's video return 404. |
| P1 | low | `email.sync.account-idor-missing-ownership-check` | mint22 | Email test: manual sync on another user's account id returns 404 and performs no provider call. |
| P1 | low | `injection/email-ui/body-html-markupstring-unsanitized` | mint22 | Component test: an inbound email body containing `<script>`, `onerror` and a `javascript:` link renders no executable markup and no script-bearing attribute. |
| P1 | low | `photos-slideshow-album-photo-ids-missing-owner-check` | mint22 | Photos test: slideshow for an album the caller cannot read returns 404 and no photo ids. |
| P2 | - | `authz.subsonic.no-credential-validation` | mint22 | Owner-observed then test: an unauthenticated Subsonic call returns the protocol auth error and never reaches an owner-filtered query with Guid.Empty. |
| P2 | - | `cli/root-backup-artifacts-default-umask-secret-exposure` | mint22 | Backup test: the archive and manifest are created with 0600 and the directory with 0700 regardless of umask. |
| P2 | - | `cli/root-cli-trusts-service-writable-config-json` | mint22 | CLI test: a config path outside the root-owned directory (or a symlink) is refused before any privileged write. |
| P2 | - | `client-token-store/predictable-machine-key-no-file-acl` | monolith | Token-store test: the key file is DPAPI/Keychain-protected and owner-only; a second OS user cannot decrypt the refresh token. |
| P2 | - | `crypto/oidc-key-custody-windows-programdata-acl` | mint22 | Installer test: the OIDC key directory and config are readable only by the service identity and administrators. |
| P2 | - | `crypto/oidc-key-rotation-ordering-legacy-filename` | mint22 | Rotation test: with mixed legacy and dated filenames, the newest key is selected by parsed date, not culture-sensitive string order. |
| P2 | - | `files-upload-content-length-allocation` | mint22 | Owner-observed then test: an over-limit Content-Length is rejected before the action allocates, or the buffer is capped at a configured chunk size. |
| P2 | - | `files-zip-entry-name-traversal-on-download` | mint22 | Download test: a folder named `..` produces archive entries whose names contain no parent segment. |
| P2 | - | `supply-chain/installer-verify-then-extract-tmp-predictable` | mint22 | Installer test: extraction uses a root-owned private directory created exclusively; a pre-created path is rejected. |

## Workstreams

### Workstream 1 — Module transport identity (gRPC)

- **Effort:** 2–3 days. Requires a module credential at spawn plus registry validation and end-to-end tests; touches core, supervisor and all module hosts.
- **Findings:** 2

#### ☐ grpc/core-capabilities/self-asserted-module-id — high

- **Title:** Core capability gRPC accepts any non-empty self-asserted module-id and trusts caller identity headers
- **Location:** `src/Core/DotNetCloud.Core.Server/Grpc/Configuration/GrpcServerConfiguration.cs:18` → `src/Core/DotNetCloud.Core.Server/Grpc/Services/GrpcHealthServiceImpl.cs:466`
- **Owner:** mint22
- **Fix:** Have ProcessSupervisor mint a per-module secret (or mTLS client certificate) at launch, pass it only to that module and validate it in AuthenticationInterceptor against the installed-module record so the module id is bound to a credential rather than read from a header. Derive CallerContext from that verified identity plus a server-verified end-user context, and replace the CallerType.System default with an explicit supervisor-only identity. Enforce ModuleCapabilityGrants per RPC by calling CapabilityValidator and throwing CapabilityNotGrantedException for Restricted/Privileged capabilities, and move admin-only RPCs such as CleanupAdminSharedFolder off the general module surface or require a specific privileged grant.
- **Why:** A caller with no credential reads every user's email address, writes notifications and real-time UI events into arbitrary users' sessions, forges the audit trail used for SOC 2 CC4 attribution, and runs a destructive admin cleanup against other users' media settings; the module capability-grant model is bypassed entire...
- **Regression test:** gRPC integration test: a call with no module credential is rejected; a valid module credential limiter still cannot set `caller-user-id`/`caller-type` to another principal or to System.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ grpc/module-hosts/request-user-id-without-peer-auth — high

- **Title:** Module host data-plane gRPC services are anonymous and act on a request-supplied user id
- **Location:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Program.cs:47` → `src/Core/DotNetCloud.Core.Server/Supervisor/GrpcChannelManager.cs:72`
- **Owner:** mint22
- **Fix:** Authenticate the core-to-module channel once and bind the request user id to it: have the core attach a supervisor-issued per-call credential (or mTLS) and add a module gRPC interceptor that rejects unauthenticated calls, then require every service to compare request.UserId with the authenticated principal exactly as FilesGrpcService.ValidateAuthenticatedCaller does. Alternatively, drop user_id from the request contract and derive the acting user solely from the verified caller context. Also stop using CallerContext.CreateSystemContext() for user-scoped reads and writes such as Email GetAccount, GetRecentThreads and SendEmail.
- **Why:** Cross-user read of email threads and message bodies, chat messages and calendar data, plus writes: messages attributed to any user in any channel and mail sent from another user's account. The documented rule that modules cannot access each other's data directly is fully defeated.
- **Regression test:** Per-host gRPC test in the style of `FilesGrpcServiceSecurityTests`: a request whose JWT subject differs from `request.UserId` returns NotFound/PermissionDenied for every module service.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 2 — Credential state, revocation and scope

- **Effort:** 1–2 days. Security stamp, token revocation on state change, `IsActive` at the credential-acceptance point, antiforgery on session endpoints, audience/scope binding.
- **Findings:** 6

#### ☐ core-auth:account-state-transition-no-token-revocation — high

- **Title:** Disabling an account revokes nothing: the sliding cookie session, access tokens and refresh tokens all survive
- **Location:** `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs:188` → `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs:99`
- **Owner:** mint22
- **Fix:** Add one account-state invalidation step and call it from every transition: in DisableUserAsync (and DeleteUserAsync, AdminResetPasswordAsync, ResetPasswordAsync, ChangePasswordAsync) enumerate IOpenIddictTokenManager.FindBySubjectAsync(userId) and TryRevokeAsync each entry, and call UserManager.UpdateSecurityStampAsync so Identity rejects existing cookie tickets. Enable OpenIddict token-entry validation (or add an IsActive check to the claims transformation) so a token that escapes revocation still fails on the next request. Regression case: obtain a cookie, an access token and a refresh token, disable the account, then assert 401 on all three.
- **Why:** The disable action - the primary containment step after credential compromise - is fully defeated: the principal keeps access to its files, mail, chat and shares and can mint new access tokens from an unrevoked refresh token.
- **Regression test:** Integration test: disable a user, then reuse an access token and a refresh token issued before the change — both must fail; the cookie session must not renew.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ core-session:disabled-account-form-login — high

- **Title:** Form login (POST /auth/session/login) accepts a disabled account and issues a full session
- **Location:** `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Login.razor:14` → `src/Core/DotNetCloud.Core.Auth/Services/AuthService.cs:286`
- **Owner:** mint22
- **Fix:** Enforce the predicate at the single credential-acceptance point: look up the user before sign-in and, when IsActive is false, return the same generic invalid-credentials redirect used for a wrong password. Better, move the check into a shared helper or a custom SignInManager/IUserValidator wrapper so AuthService.LoginAsync, the form login, MFA verification and future login paths inherit it. Regression case: disable a user, post the form login, assert no Set-Cookie and a generic error.
- **Why:** The explicit disable control is fully defeated: the disabled principal regains a complete authenticated session and, through it, its data and the authorization-code flow.
- **Regression test:** `AuthSessionController` test: POST the login form for an inactive user returns the failure view and sets no auth cookie.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ core-auth/claims-transformation/admin-perm-claim-cache-not-invalidated-on-role-removal — medium

- **Title:** Demoted administrator keeps admin authority for up to five minutes through the cached claims transformation and can re-grant the role
- **Location:** `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs:282` → `src/Core/DotNetCloud.Core.Server/Controllers/UserManagementController.cs:302`
- **Owner:** mint22
- **Fix:** Invalidate the per-user claims entry whenever authority or account state changes: expose an invalidation call (IMemoryCache.Remove($"claims:{userId}") behind a small interface implemented by the transformation) and invoke it from SetUserRolesAsync, RemoveUserRoleAsync, DisableUserAsync, EnableUserAsync, DeleteUserAsync and AdminResetPasswordAsync, ideally via the role-change event so out-of-process changes also clear it. Additionally, stop authorizing on cached privilege claims - re-read the Administrator role (or a per-user role version stamp) in PermissionAuthorizationHandler, or exclude dnc:perm and role claims from the cached list - and call UpdateSecurityStampAsync on role removal so the ticket is rejected at the next revalidation. Regression case: sign in as an admin, demote that account from a second session, then immediately call a RequireAdmin endpoint with the first session and assert 403.
- **Why:** The explicit demotion control is defeated for a bounded window: the whole administrative surface is briefly retained and the demoted principal can restore the role permanently, but the window is short and requires a pre-existing live session.
- **Regression test:** Authorization test: removing Administrator from a user invalidates the cached `dnc:perm=admin` claim on the next request (or within a documented, tested window).
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ core-session:login-form-csrf — medium

- **Title:** Login form POST has no antiforgery validation, enabling cross-site login CSRF
- **Location:** `src/UI/DotNetCloud.UI.Web/Components/Pages/Auth/Login.razor:14` → `src/Core/DotNetCloud.Core.Server/Controllers/AuthSessionController.cs:256`
- **Owner:** mint22
- **Fix:** Apply AutoValidateAntiforgeryTokenAttribute as a global MVC filter (or decorate AuthSessionController and every cookie-authenticated form action with ValidateAntiForgeryToken) and render the token in the login and logout forms; for endpoints that must remain token-free, add an explicit same-origin Origin/Referer check. Regression case: post the login form without a token from a foreign Origin and assert 400 with no Set-Cookie.
- **Why:** Cross-principal consequence: the victim's later work is created inside and readable by the attacker's account, but the attacker gains no access to the victim's existing data.
- **Regression test:** A login/logout POST without a valid antiforgery token (or with a foreign Origin) is rejected; the happy path still signs in.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ oauth-revocation/module-introspection-accepts-revoked-access-token — medium

- **Title:** Module hosts keep accepting access tokens after logout revoked them, for the token's full remaining lifetime
- **Location:** `src/Core/DotNetCloud.Core.Server/Controllers/AuthController.cs:131` → `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:21`
- **Owner:** mint22
- **Fix:** Make module bearer validation state-aware. In TokenIntrospectionServiceImpl resolve the presented token through IOpenIddictTokenManager (and its authorization) and return active=false when the entry is revoked or expired, and reject tokens whose authorization no longer grants the scopes. Keep the handler cache very short or invalidate it on a core-side 401, and consider issuing reference/opaque access tokens to modules so revocation is observed on every call.
- **Why:** Up to the remaining access-token lifetime (60 minutes by default) of full user-level access to the bearer-enabled module APIs after revocation, with no ability to extend it beyond the token's own expiry and no effect on the core's own endpoints.
- **Regression test:** Module test: after revoking an access token in the core, the module rejects it within the documented cache window (or immediately with token-entry validation).
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ oauth-scope/module-introspection-ignores-scope-and-audience — medium

- **Title:** Module token introspection enforces neither scope nor audience, so a bookmarks-only consent token grants full user authority on files, chat, notes, calendar, music and AI
- **Location:** `src/Core/DotNetCloud.Core.Server/Program.cs:1198` → `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:21`
- **Owner:** mint22
- **Fix:** Make scope and audience real at the module boundary: have TokenIntrospectionServiceImpl validate audience against a resource identifier for the calling module and return a scope set limited to that module's declared scopes, and add a scope requirement to every module endpoint (a policy over the 'scope' claims, e.g. files:read/files:write for Files, bookmarks:read/write for Bookmarks). Reject tokens whose scope set does not include the module's declared scopes, and stop relying on client permission sets as the only restriction.
- **Why:** The narrow-consent token gains the user's full module authority - file read/write, chat, notes, calendar, music and billable AI calls - but the blast radius is one account's data and requires prior token possession, not a cross-user or tenant breach.
- **Regression test:** Module authorization test: a token minted with only `files:read` cannot call a write endpoint; an audience-restricted token is rejected by the wrong module.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 3 — Files write path, quotas and secret exposure

- **Effort:** 1–2 days. One write-permission predicate, fail-closed grant resolution, quota recompute, one path-containment helper, one stream entitlement check, key rotation.
- **Findings:** 7

#### ☐ authz.video.hls-segment.no-video-entitlement — high

- **Title:** HLS playlist/segment route serves any video's transcoded stream to any authenticated session
- **Location:** `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs:1229` → `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs:1320`
- **Owner:** mint22
- **Fix:** Require the stream-token path by default and issue tokens only after GetVideoForStreamingAsync(videoId, userId) succeeds; in the cookie branch perform the same entitlement lookup before serving, key or validate the HLS job by (videoId, userId), and add a regression test asserting 403/404 for an authenticated non-owner.
- **Why:** Full disclosure of another user's private video content - playlist and all segments - with no share relationship, and the reader can hold the transcode job open.
- **Regression test:** Video test: user B requesting user A's HLS playlist and segment returns 404/403; a valid stream token for A still works.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ email.attachment.storage-key-path-traversal — high

- **Title:** Client-supplied email attachment storage key is resolved as a filesystem path, letting any authenticated user read arbitrary server files
- **Location:** `src/Modules/Email/DotNetCloud.Modules.Email.Host/Controllers/EmailController.cs:153` → `src/Modules/Email/DotNetCloud.Modules.Email.Host/Controllers/EmailController.cs:535`
- **Owner:** mint22
- **Fix:** Validate the key with a strict ^[0-9a-f]{64}$ regex and additionally resolve with Path.GetFullPath and require the result to start with the full storage root plus a separator, inside GetStoragePath/TryGetStoragePath so every caller (OpenReadAsync, GetSizeAsync, DeleteAsync, GetCreationTime) is covered; return null/0/false for invalid keys in the read helpers, reject non-conforming keys in EmailSendService before the provider is invoked so no EmailAttachment row can hold a client path, and add a regression test asserting a send with storageKey '../../../../../etc/passwd' is rejected with 400 and that OpenReadAsync returns null for any non-hex key.
- **Why:** Arbitrary file read as the email module process, which shares the Data Protection key ring directory with the core process; session-cookie protection keys, per-user mail credential blobs, other users' attachment content and module configuration (including the database connection string) are in reach.
- **Regression test:** Email host test: attachment key `../../appsettings.json` and an absolute path both return 404/400 and never read outside the attachment root.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ files-admin-shared-folder-group-grant-fail-open — high

- **Title:** Admin shared-folder group grants fail open in the Files module host, exposing every mounted folder to any authenticated user
- **Location:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:89` → `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:450`
- **Owner:** mint22
- **Fix:** Delete the fail-open branches: in FileService.ListAccessibleAdminSharedFoldersAsync and both DownloadService lookups, treat a null or empty membership result as no accessible folders (return an empty list or null) and always apply definition.Grants.Any(grant => groupIds.Contains(grant.GroupId)). Separately implement GrpcGroupDirectory.GetGroupsForUserAsync against the CoreCapabilities gRPC group listing (or another real directory) so grants work in the module host, and unit-test that an ungrouped caller sees zero admin shared folders.
- **Why:** Discloses the contents of server-local directories explicitly scoped to specific groups to every authenticated user, including downloading the underlying host files.
- **Regression test:** Admin shared-folder test: an authenticated user with no matching grant does not see the mounted folder; a missing/empty group resolution denies by default.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ files-chunk-storage-quota-bypass — high

- **Title:** Chunk uploads store bytes far beyond the accounted quota because only the client-declared TotalSize is reserved, recorded and recalculated
- **Location:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:319` → `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/QuotaService.cs:251`
- **Owner:** mint22
- **Fix:** Make stored bytes the single source of truth. Add FileUploadOptions.MaxChunkSizeBytes and reject any chunk whose Content-Length or read length exceeds it; in UploadChunkAsync reject the chunk when accumulated stored bytes for the session would exceed TotalSize plus the user's remaining quota; in InitiateUploadAsync require ChunkHashes.Count and (when present) ChunkSizes.Count to match and their sum to equal TotalSize; in CompleteUploadAsync set FileNode.Size and the quota delta from the sum of the referenced chunk sizes rather than session.TotalSize. Regression: initiate TotalSize = 1 with four 16 MiB chunks and assert completion is rejected (or that UsedBytes and FileNode.Size increase by the real 64 MiB) and no chunk newer than the session remains unreferenced.
- **Why:** The excess data lands on the shared data volume used for all users' chunks, temp ZIP assembly and backups, so exhausting it degrades or stops uploads, downloads and backups for every user and module; the referenced chunks are exempt from the orphan GC and the quota control is silently invalid.
- **Regression test:** Quota test: completing chunks whose stored bytes exceed the user quota is rejected; usage is recomputed from stored bytes, not the declared TotalSize.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ files-chunked-upload-missing-target-write-authorization — high

- **Title:** Chunked upload accepts any target parent or file node and writes into another user's tree without a permission check
- **Location:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:319` → `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ChunkedUploadService.cs:349`
- **Owner:** mint22
- **Fix:** Inject IPermissionService into ChunkedUploadService and call RequirePermissionAsync(dto.ParentId, caller, SharePermission.ReadWrite) when ParentId is set (and on the caller's root when null) and RequirePermissionAsync(dto.TargetFileNodeId.Value, caller, SharePermission.ReadWrite) when TargetFileNodeId is set; repeat the parent check in CompleteUploadAsync before mutating anything. Keep the mounted/read-only guards. Add tests asserting a Read-only sharee cannot initiate or complete an upload into the shared folder and that a same-named file owned by the folder owner is not overwritten.
- **Why:** Arbitrary cross-user file creation plus in-place replacement of another user's file content and a new version attributed to the attacker, with the victim's version history affected; a read-only share is escalated to write.
- **Regression test:** `ChunkedUploadService` test: user B completing an upload into user A's folder returns PermissionDenied and leaves A's node unchanged.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ files/public-share/max-downloads-toctou@ShareService.cs:279+328 — medium

- **Title:** Public-link MaxDownloads cap is enforced by a non-atomic check-then-increment, so concurrent downloads exceed the owner's limit
- **Location:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/PublicShareController.cs:54` → `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/ShareService.cs:328`
- **Owner:** mint22
- **Fix:** Consume the quota atomically at delivery. Replace the separate check and increment with one conditional update, e.g. FileShares.Where(s => s.Id == shareId && (s.MaxDownloads == null \|\| s.DownloadCount < s.MaxDownloads)).ExecuteUpdateAsync(s => s.SetProperty(x => x.DownloadCount, x => x.DownloadCount + 1)), treat zero affected rows as the limit being reached and return the not-found/maxed response, and only start streaming after the update succeeds. Alternatively mark DownloadCount as a concurrency token or add a row version and retry on conflict, and keep the resolve-time check only as a fast path. Add a regression test using two independent DbContext scopes that both resolve a MaxDownloads = 1 share before either increments.
- **Why:** The owner's explicit download cap is exceeded, the counter permanently undercounts, and repeated concurrency keeps a capped link usable; blast radius is limited to the one shared file and requires possession of the link token or password.
- **Regression test:** Concurrency test: N parallel downloads against a MaxDownloads=1 share result in exactly one success (conditional update or concurrency token).
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ secret-disclosure/tmdb-api-key-committed-installer — low

- **Title:** TMDB API v3 key committed in source and written into every systemd unit as the default enrichment credential
- **Location:** `tools/install.sh:712` → `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoSettingsProvider.cs:48`
- **Owner:** mint22
- **Fix:** Remove the literal from source and from both unit templates, default the setting to empty so operators must supply their own key (empty disables enrichment), rotate the exposed key at TMDB, and add third-party-key detection to CI secret scanning.
- **Why:** Exposure yields third-party TMDB quota/attribution abuse and shared-key reputational risk, not access to DotNetCloud data, accounts or signing keys.
- **Regression test:** Rotate the key at the provider, remove it from `tools/install.sh`, and assert in CI that no provider key literal is present in the tree.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 4 — Missing per-resource authorization predicates

- **Effort:** 2–3 days. Repetitive predicate work; each service needs an owner/product filter and a negative test.
- **Findings:** 9

#### ☐ tracks-rest-workitem-object-idor-missing-product-membership — high

- **Title:** Tracks work-item REST endpoints authorize on authentication only, never on product membership
- **Location:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/WorkItemsController.cs:113` → `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/GuestAccessService.cs:102`
- **Owner:** mint22
- **Fix:** Add one authorization helper that resolves the resource's ProductId and requires an active ProductMembers row for the caller (with role checks for delete and administrative verbs), and call it from every Tracks controller before invoking the service; alternatively pass CallerContext into the services and apply the predicate in the query. Add regression tests with two products owned by different users asserting 403/404 on cross-product GET/PUT/DELETE/attachment/guest reads and that the victim row is unchanged.
- **Why:** Cross-tenant confidentiality (work items, attachments, guest PII) and integrity (item update and soft delete, attachment deletion) are demonstrated by the missing predicate.
- **Regression test:** Tracks test: a non-member of a product gets 404/403 for work-item read, update, delete and attachment read.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ tracks/agile-services-missing-product-membership-predicate — high

- **Title:** Tracks analytics, sprint, poker, time-tracking and review-session endpoints authorize on authentication only, exposing and mutating any product's agile data
- **Location:** `src/Core/DotNetCloud.Core.Server/Program.cs:1271` → `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Data/Services/SprintService.cs:88`
- **Owner:** mint22
- **Fix:** Thread CallerContext through the Tracks agile service layer and add one product-scope guard: resolve the owning product id (productId, sprint.Epic.ProductId, poker/ReviewSession EpicId, workItem.ProductId, entry -> workItem.ProductId) and require ProductService.GetUserProductRoleAsync to return a role before any read or write, throwing ForbiddenException(ErrorCodes.NotBoardMember) otherwise, with Admin or Owner required for destructive or host-only actions such as sprint delete/complete, review-session current-item/end and poker accept. Bind owner-scoped rows directly to the caller (time-entry read and delete require entry.UserId == caller.UserId unless Admin/Owner) and validate that the route itemId matches the entry's WorkItemId. Enforce the guard centrally, for example an IAsyncActionFilter or a TracksAccessService applied by convention, so new controllers cannot omit it. Filter ListProductsByOrganizationAsync by ProductMembers for the caller, verify organization membership through the existing IOrganizationDirectory capability, and return Forbid rather than Unauthorized for authorization failures. Cover the change with service-level tests that create two products with disjoint ProductMembers and assert every analytics/sprint/poker/time-entry/review-session entry point throws Forbidden for the non-member, succeeds for a member, and stops succeeding after membership removal.
- **Why:** Cross-product confidentiality of work-item titles, due dates, sprint goals, velocity and per-user workload, cross-user personal data (time entries with descriptions and durations), and integrity effects: deleting time entries, deleting or completing sprints, overwriting StoryPoints via a forged accepted estimate, and e...
- **Regression test:** Tracks test: non-member cannot read analytics/burndown, sprint contents, poker sessions, time entries or review sessions, and cannot delete/complete them.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ authz.video.screenshot.no-video-entitlement — medium

- **Title:** Video screenshot route reads any video's generated frames without an ownership check
- **Location:** `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs:131` → `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs:139`
- **Owner:** mint22
- **Fix:** Resolve the caller in GetScreenshot and verify entitlement with GetVideoAsync(videoId, caller) or the streaming owner predicate before enumerating frames; add the caller parameter to the thumbnail service so the check cannot be omitted, and add a regression test asserting 404 for an authenticated non-owner.
- **Why:** Cross-user disclosure of content frames from another user's private video, limited to the generated JPEG stills rather than the full media.
- **Regression test:** Video test: the screenshot route resolves the caller and returns 404 for a video the caller does not own.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ bookmarks-preview-endpoints-missing-owner-check — medium

- **Title:** Bookmark rich-preview endpoints expose and can rewrite other users' bookmark metadata
- **Location:** `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/Controllers/BookmarksController.cs:288` → `src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Data/Services/BookmarkPreviewFetchService.cs:123`
- **Owner:** mint22
- **Fix:** Change the preview service signatures to accept CallerContext, load the bookmark with OwnerId == caller.UserId, and return 404 otherwise; never expose BookmarkPreview without the same predicate. Regression tests: cross-user fetch and read return 404 and leave the victim bookmark Title unchanged.
- **Why:** Cross-user disclosure of the bookmarked page's canonical URL, site name, title, description and image/favicon URLs, plus a side-effect write to the victim bookmark's title.
- **Regression test:** Bookmarks test: preview read/refresh for another user's bookmark returns 404 and does not rewrite its metadata.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ chat-uploads-anonymous-read:no-channel-membership-binding:no-expiry-and-no-revocation — medium

- **Title:** Anonymous chat upload read endpoint serves channel and DM images to anyone holding the URL, with no expiry or revocation and no payload deletion
- **Location:** `src/Core/DotNetCloud.Core.Server/Program.cs:1198` → `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/LocalChatImageStore.cs:88`
- **Owner:** mint22
- **Fix:** Move the read decision to request time and make the reference bounded. (1) Replace the anonymous action with an authenticated action that resolves the referencing message and channel and re-checks current membership (for example GET /api/v1/chat/channels/{channelId:guid}/attachments/{fileName}); if unauthenticated <img> embedding is required, use an HMAC over {channelId, fileName, expiry} verified before the store is called. (2) Record each upload in the database (channel, message, uploader, created_at, content hash) and delete the payload when the message or channel is deleted, when retention purges the message, and via an orphan sweep. (3) Have the attachment reference a server-side upload id rather than accepting a client-supplied ThumbnailUrl. (4) Remove image/svg+xml from the allowlist or serve it as an attachment from a sandboxed origin, and validate type from magic bytes. (5) Add a negative test asserting every Chat REST action except the intended public ones is unreachable without a caller context.
- **Why:** Confidential channel and direct-message images become readable by unauthenticated third parties and remain readable after membership loss, message deletion and retention purge; scope is one object per leaked reference, with no write or availability effect.
- **Regression test:** Chat test: an unauthenticated read of an upload URL whose channel the caller cannot see returns 404; a signed, expiring URL works only for its channel.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ photos-thumbnail-endpoint-missing-owner-or-share-check — medium

- **Title:** Photo thumbnail endpoint serves any stored photo thumbnail to any authenticated user
- **Location:** `src/Modules/Photos/DotNetCloud.Modules.Photos.Host/Controllers/PhotosController.cs:78` → `src/Modules/Photos/DotNetCloud.Modules.Photos.Data/Services/PhotoThumbnailService.cs:114`
- **Owner:** mint22
- **Fix:** Pass CallerContext into the thumbnail path and call PhotoService.GetPhotoAsync(photoId, caller) before reading the blob (or add the equivalent owner-or-unexpired-share predicate and drop IgnoreQueryFilters for request-time reads). Regression tests: user B requesting user A's photo thumbnail, and a soft-deleted photo thumbnail, must both return 404.
- **Why:** Confidentiality of another user's image content as a downscaled derivative (grid up to 1024 px, detail up to 4096 px configurable, default 1200 px), including soft-deleted photos; no mutation and not the full-resolution original.
- **Regression test:** Photos test: thumbnail of another user's photo and of a soft-deleted photo both return 404; a shared photo still returns the thumbnail.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ video-screenshot-subtitle-metadata-missing-user-video-ownership — medium

- **Title:** Video subtitle and metadata endpoints do not bind the id to the caller's UserVideo ownership
- **Location:** `src/Modules/Video/DotNetCloud.Modules.Video.Host/Controllers/VideoController.cs:343` → `src/Modules/Video/DotNetCloud.Modules.Video.Data/Services/VideoMetadataService.cs:85`
- **Owner:** mint22
- **Fix:** Resolve the UserVideo with OwnerId == caller.UserId in every subtitle and metadata action (or pass CallerContext into the services and apply the predicate there); gate subtitle delete and metadata save on ownership of the associated UserVideo. Regression tests: cross-user subtitle listing/content read/delete and metadata read/save return 404/403 and leave canonical rows untouched.
- **Why:** Disclosure of another user's subtitle text and, for shared content hashes, deletion or tampering of canonical subtitle and metadata rows with limited blast radius.
- **Regression test:** Video test: screenshot, subtitle and metadata of another user's video return 404.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ email.sync.account-idor-missing-ownership-check — low

- **Title:** Manual email sync endpoint acts on any account id without checking that the caller owns it
- **Location:** `src/Modules/Email/DotNetCloud.Modules.Email.Host/Controllers/EmailController.cs:185` → `src/Modules/Email/DotNetCloud.Modules.Email.Data/Services/EmailSyncBackgroundService.cs:122`
- **Owner:** mint22
- **Fix:** Change IEmailSyncService.SyncAccountAsync to accept a CallerContext (or an owner id) and resolve the account with a.Id == accountId && a.OwnerId == caller.UserId && !a.IsDeleted, returning EmailAccountNotFound (404) otherwise; have the controller call GetAuthenticatedCaller() first. Add a regression test where user B calls sync on user A's account and receives 404 with no provider connection attempted.
- **Why:** No data is disclosed to the attacker; the effect is unauthorized state change and resource use on the victim's mailbox (external provider connections, quota consumption, mailbox/message upserts, and unbounded concurrent syncs) that the victim's own schedule already performs.
- **Regression test:** Email test: manual sync on another user's account id returns 404 and performs no provider call.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ photos-slideshow-album-photo-ids-missing-owner-check — low

- **Title:** Slideshow-from-album returns the photo ids of any user's album
- **Location:** `src/Modules/Photos/DotNetCloud.Modules.Photos.Host/Controllers/PhotosController.cs:369` → `src/Modules/Photos/DotNetCloud.Modules.Photos.Data/Services/SlideshowService.cs:31`
- **Owner:** mint22
- **Fix:** Add CallerContext to CreateFromAlbumAsync and require album.OwnerId == caller.UserId or HasAlbumShareAccessAsync before enumerating AlbumPhotos. Regression test: cross-user slideshow creation returns 404 and no photo ids.
- **Why:** Discloses album membership as an ordered list of photo ids and a count, not image bytes; it also supplies the identifiers for the separate thumbnail IDOR.
- **Regression test:** Photos test: slideshow for an album the caller cannot read returns 404 and no photo ids.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 5 — Realtime hub authorization

- **Effort:** 1–2 days. Fix the membership call, implement the revocation path, mirror REST rules in the hubs.
- **Findings:** 4

#### ☐ access-control/core-hub/channel-group-membership-never-revoked — high

- **Title:** Channel SignalR group membership is cached per user and no revocation path reaches the core
- **Location:** `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs:140` → `src/Core/DotNetCloud.Core.Server/RealTime/RealtimeBroadcasterService.cs:38`
- **Owner:** mint22
- **Fix:** Add a revocation path that reaches the core: either a CoreCapabilities RPC (for example LeaveGroup(userId, group)) implemented by RealtimeBroadcasterService, or a core-side handler for UserLeftChannelEvent/ChannelDeletedEvent that calls IRealtimeBroadcaster.RemoveFromGroupAsync. Additionally re-verify channel membership for each retained group in OnConnectedAsync, or drop retained memberships on disconnect. Add a regression test that a removed member's reconnect does not re-join the channel group.
- **Why:** Revoked access is not enforced: the user keeps receiving confidential channel content indefinitely, on every device and after every reconnect.
- **Regression test:** SignalR test: after leaving/being removed from a channel, no further channel events are delivered to that connection.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ access-control/core-hub/join-group-channel-membership-noop — high

- **Title:** SignalR channel-group join authorizes on channel existence instead of channel membership
- **Location:** `src/Core/DotNetCloud.Core.Server/RealTime/CoreHub.cs:140` → `src/Core/DotNetCloud.Core.Server/RealTime/ChatHub.cs:63`
- **Owner:** mint22
- **Fix:** Implement a real membership predicate in the Chat gRPC surface (for example GetChannelForUser(channelId, userId) filtering ChannelMembers, or a dedicated IsChannelMember RPC) and have ChatGrpcApiClient.IsChannelMemberAsync call it instead of GetChannelAsync. Never treat channel existence as membership. Add a regression test asserting IsChannelMemberAsync returns false for an existing channel the caller does not belong to and that no group join occurs.
- **Why:** Cross-user read of private channel message content, edits and deletions by a non-member for as long as the connection stays open.
- **Regression test:** SignalR test: joining another user's channel group does not deliver that channel's messages; the membership check returns false for a non-member.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ access-control/chat-hub/delete-message-no-owner-check — medium

- **Title:** ChatHub message deletion accepts any message ID and ignores the caller identity
- **Location:** `src/Core/DotNetCloud.Core.Server/RealTime/ChatHub.cs:105` → `src/Modules/Chat/DotNetCloud.Modules.Chat.Host/Services/ChatGrpcService.cs:275`
- **Owner:** mint22
- **Fix:** Move deletion behind MessageService.DeleteMessageAsync or replicate its predicate in ChatGrpcService.DeleteMessage: allow only SenderUserId == request.UserId, or an active ChannelMember with admin/owner role. Add a regression test asserting a non-owner, non-member caller gets Success=false and the row is unchanged.
- **Why:** Cross-user destructive integrity loss (message removal) in arbitrary channels; the soft delete limits recovery effort but the content still disappears for members.
- **Regression test:** Hub test: deleting another user's message without the same role rules as REST is rejected.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ access-control/chat-hub/send-message-no-channel-membership — medium

- **Title:** ChatHub message send skips the channel-membership and DM block checks enforced on the REST path
- **Location:** `src/Core/DotNetCloud.Core.Server/RealTime/ChatHub.cs:49` → `src/Core/DotNetCloud.Core.Server/RealTime/ChatHub.cs:63`
- **Owner:** mint22
- **Fix:** Route hub sends through MessageService.SendMessageAsync, or replicate its membership and DM-block predicates in ChatGrpcService.SendMessage and return Success=false with a generic error for non-members. Add regression tests for a non-member send and a blocked-DM send.
- **Why:** Cross-user integrity and abuse path: arbitrary content injected into other users' channels, bypassing both the membership model and the DM block list.
- **Regression test:** Hub test: sending to a channel the caller has not joined (or is blocked from) is rejected, matching the REST path.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 6 — Client-side path confinement

- **Effort:** 1 day. Canonicalize and re-check the joined path after every server-supplied name.
- **Findings:** 1

#### ☐ client-sync/tree-name-path-escape-outside-sync-root — high

- **Title:** Desktop sync client writes server-named paths outside the sync root (unvalidated tree paths)
- **Location:** `src/Clients/DotNetCloud.Client.Core/Api/DotNetCloudApiClient.cs:517` → `src/Clients/DotNetCloud.Client.Core/Sync/SyncEngine.cs:1280`
- **Owner:** monolith
- **Fix:** Reject unsafe remote names at ingest (no path separators, no '.', '..', no rooted/absolute form, no Windows reserved names or trailing dots/spaces) inside BuildPathMap/BuildServerFileMap/PruneNode/BuildFolderPathMap, and route every server-derived path through ValidatePathWithinSyncRoot before use, specifically SyncEngine.cs:1486 (queue time) and 1279-1280 (folder creation), plus the FUSE and Cloud Filter placeholder builders; add regression tests with a tree containing '..' and absolute names asserting no filesystem mutation outside the root.
- **Why:** Arbitrary file create/overwrite as the sync user outside the declared sync folder, with attacker-controlled content, enables code execution (autostart, shell profile) and local configuration tampering.
- **Regression test:** `Client.Core` test: server-supplied names `..\\..\\evil.txt`, `C:evil.txt`, `CON`, trailing-dot and long paths all resolve inside the sync root or are rejected.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 7 — Rendering, search ACLs, outbound fetch and queues

- **Effort:** 1–2 days across four small changes plus an HTML sanitizer decision.
- **Findings:** 5

#### ☐ ai-completion-queue-unbounded-fifo — medium

- **Title:** Single global AI inference queue has no depth bound or per-user fairness, so one user can monopolize the shared LLM worker
- **Location:** `src/Modules/AI/DotNetCloud.Modules.AI.Host/Controllers/AiChatController.cs:138` → `src/Modules/AI/DotNetCloud.Modules.AI.Data/Services/AiCompletionQueue.cs:141`
- **Owner:** mint22
- **Fix:** Bound and share the queue: cap total depth and per-user in-flight plus queued items in AiCompletionQueue.Enqueue, reject with a retry hint (HTTP 429 for the SSE endpoint, RESOURCE_EXHAUSTED for gRPC) once a cap is reached, and serve round-robin or with per-user lanes instead of one global FIFO. Add a per-user rate limit on the AI routes and a message/history length cap in BuildLlmRequest. Regression case: enqueue N items from user A and one from user B, assert B is served after at most one A item, and assert that exceeding the per-user cap is rejected rather than queued.
- **Why:** The single shared inference worker is occupied by the attacker's FIFO backlog, so every other user's completions are delayed or unavailable while it drains, and the queue retains each item's full prompt history in module memory; other modules are unaffected and a process restart clears the backlog.
- **Regression test:** Queue test: a per-user in-flight cap and a global depth cap reject rather than enqueue beyond the bound, with a clear error.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ search-index/calendar-creator-owner-acl-drift — medium

- **Title:** Calendar events are indexed under their creator, so a revoked sharee keeps receiving another owner's event content from full-text search
- **Location:** `src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/Controllers/CalendarController.cs:153` → `src/Core/DotNetCloud.Core.Search/Services/PostgreSqlSearchProvider.cs:164`
- **Owner:** mint22
- **Fix:** Stop treating one ingest-time owner as the ACL, and close the producer gap. (1) In MapEventToSearchableDocument index the calendar's ACL owner (Calendar.OwnerId, or a defined organization principal) instead of the event creator, carrying that value through the ListAllEventsAsync/GetSearchableDocument projection. (2) Invalidate the derived copy on every grant change: publish SearchIndexRequestEvent(Remove) for the calendar's events from CalendarShareService.RemoveShareAsync, from team and organization membership removal, and from calendar deletion, and add a core handler that purges entries for a deleted user. (3) Add a retrieval-time check that re-validates the caller's current read grant per (ModuleId, EntityId) through the owning module so no stale copy is authoritative. Regression test: create an event as a sharee, revoke the share, run a full reindex, then assert the search returns zero items for the revoked user and one item for the calendar owner.
- **Why:** A user whose access was revoked can read the title, a description/location snippet and the last-modified time of events they created in another principal's calendar, and keeps receiving that calendar's later edits on each reindex; the blast radius is confined to that event family inside one calendar and grants no acces...
- **Regression test:** Search test: after revoking a calendar share, a search by the former sharee no longer returns the event; a full reindex does not re-add it.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ ssrf.safe-url-fetcher.dns-rebind-toctou — medium

- **Title:** Preview SSRF guard validates a DNS answer it then discards and connects by hostname (DNS-rebind check/use gap)
- **Location:** `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/MessageService.cs:174` → `src/Modules/Chat/DotNetCloud.Modules.Chat.Data/Services/SafeUrlFetcher.cs:283`
- **Owner:** mint22
- **Fix:** In ConnectCallback, connect to one of the addresses just validated (new IPEndPoint(address, context.DnsEndPoint.Port)) and set the Host header and TLS SNI from the original URI instead of re-resolving by name; apply the same fix to the Bookmarks copy and consolidate both into one shared fetcher so the copies cannot diverge further.
- **Why:** Internal HTTP(S) pages become reachable from the server and their metadata (title/description/image URL) is stored and displayed, with the read capped at 768 KB and restricted to text/html; no credentials are attached.
- **Regression test:** Fetcher test with a resolver returning a public address first and a private address on connect: the connection is pinned to the validated address and the second answer is not used.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ ssrf.tracks.webhook.user-url-not-validated — medium

- **Title:** Tracks webhook delivery POSTs to a user-supplied URL with no destination guard and returns part of the upstream response
- **Location:** `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/WebhooksController.cs:54` → `src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/Controllers/WebhooksController.cs:139`
- **Owner:** mint22
- **Fix:** Route webhook delivery through a single shared SSRF-safe fetcher: allowlist http/https, resolve once and connect only to the validated IP (preserving Host/SNI), reject loopback/private/link-local/CGNAT/metadata ranges and non-80/443 ports, disable auto-redirect and re-validate each hop, cap the response size, and validate the URL at create, update and every delivery. Never return upstream response bodies or transport exception text to the caller; log status only.
- **Why:** Real network-boundary violation from the module process: internal HTTP endpoints are reached and a bounded (500-character, non-2xx only) response body plus transport error text is disclosed, and unauthenticated internal endpoints can be POSTed to; the blast radius is limited by the fixed request shape, the absence of a...
- **Regression test:** Webhook test: a URL resolving to 127.0.0.1/link-local/metadata is rejected at subscription and at delivery; a redirect to a private address is also rejected.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ injection/email-ui/body-html-markupstring-unsanitized — low

- **Title:** Inbound email HTML is rendered with MarkupString in the application origin without sanitization
- **Location:** `src/Modules/Email/DotNetCloud.Modules.Email.Data/Services/ImapSmtpEmailProvider.cs:569` → `src/Modules/Email/DotNetCloud.Modules.Email/UI/EmailPage.razor:310`
- **Owner:** mint22
- **Fix:** Sanitize email HTML at ingest (ImapSmtpEmailProvider.ExtractBodyFromMimeMessage) and again before rendering (EmailPage), using a shared sanitizer configured for email: no script/style/iframe/form/object, no event handlers, http/https/mailto/cid schemes only. Alternatively render bodies inside a sandboxed iframe with a restrictive policy. Add regression tests for script, event handler, javascript: URL, iframe and style-attribute payloads.
- **Why:** Script execution is blocked by CSP and off-site form posts by form-action, so the realistic result is in-origin UI redress/phishing overlays, same-origin framing and image tracking rather than session or account compromise.
- **Regression test:** Component test: an inbound email body containing `<script>`, `onerror` and a `javascript:` link renders no executable markup and no script-bearing attribute.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 8 — Release and installer integrity

- **Effort:** 0.5–1 day. Pin and verify the packaging tool; add a CI assertion for committed secrets.
- **Findings:** 1

#### ☐ supply-chain/appimagetool-mutable-continuous-download-unsigned — medium

- **Title:** Release job downloads and executes an unverified appimagetool from the mutable 'continuous' release while holding contents: write
- **Location:** `.github/workflows/release.yml:192` → `.github/workflows/release.yml:233`
- **Owner:** mint22
- **Fix:** Pin appimagetool to a specific release asset and verify a committed SHA-256 (or vendor it as a signed package) before execution, failing closed on mismatch; move compilation and packaging into a job with contents: read and isolate publication (contents: write) into a separate job, ideally behind a protected environment, and pin the release action to a commit SHA.
- **Why:** Code executed in the release job holds contents: write and produces the published installers, so every distributed release artifact can be altered.
- **Regression test:** Release script test: the tool download is pinned to an immutable version and verified against a recorded digest; a mismatch fails the job.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

### Workstream 9 — Validate-then-fix leads (owner observation required)

- **Effort:** Owner observation first, then small fixes. Do not schedule implementation until each blocker is resolved.
- **Findings:** 9

#### ☐ authz.subsonic.no-credential-validation — needs validation

- **Title:** Subsonic endpoints perform no credential check, fall back to a Guid.Empty caller, and leave the token validator unused
- **Location:** `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/SubsonicController.cs:16` → `src/Modules/Music/DotNetCloud.Modules.Music.Host/Controllers/SubsonicController.cs:542`
- **Owner:** mint22
- **Unresolved blocker:** Repository source binds the music host's only configured Kestrel listener to IPAddress.Loopback and the core proxy has no /rest mapping, so whether the Subsonic surface is reachable by any network principal depends on deployment facts not visible in the repository (an ASPNETCORE_URLS override, an operator reverse proxy...
- **Before fixing:** In an isolated fixture (dummy database, no external network): start the music host on loopback and record whether GET /rest/ping succeeds with no credentials; then GET /rest/getArtists with no credentials and confirm an empty index; then GET /rest/getCoverArt?...
- **Regression test:** Owner-observed then test: an unauthenticated Subsonic call returns the protocol auth error and never reaches an owner-filtered query with Guid.Empty.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ cli/root-backup-artifacts-default-umask-secret-exposure — needs validation

- **Title:** Root-run CLI backup writes credential-bearing archive and manifest without restrictive permissions
- **Location:** `src/CLI/DotNetCloud.CLI/Program.cs:35` → `src/CLI/DotNetCloud.CLI/Commands/BackupCommands.cs:368`
- **Owner:** mint22
- **Unresolved blocker:** Effective umask of the account that runs the root backup (root cron/systemd timer, or sudo re-exec from an operator shell) is a host fact; the platform default creation mode is 0666 masked by that umask, so group/other read exists only when the umask leaves those bits set (0644 under 022, 0600 under 077). Effective own...
- **Before fixing:** On an isolated Linux VM with a fixture config directory (--config-dir /tmp/dnc-fixture) whose config.json points DataDirectory at a fixture tree containing one dummy file and BackupDirectory at /tmp/dnc-fixture/out, run `umask 022; dotnetcloud backup --config-...
- **Regression test:** Backup test: the archive and manifest are created with 0600 and the directory with 0700 regardless of umask.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ cli/root-cli-trusts-service-writable-config-json — needs validation

- **Title:** Root-privileged CLI loads config.json from a service-account-owned directory and uses its paths as root filesystem targets
- **Location:** `src/CLI/DotNetCloud.CLI/Infrastructure/SystemdServiceHelper.cs:117` → `src/CLI/DotNetCloud.CLI/Commands/BackupCommands.cs:401`
- **Owner:** mint22
- **Unresolved blocker:** Actual owner and mode of /etc/dotnetcloud and /etc/dotnetcloud/config.json on the target host, and whether the service account can unlink or rename entries there: source shows the installer chown (tools/install.sh:639) and that setup tightens only the file, but the live directory DAC (and any later operator hardening) ...
- **Before fixing:** On an isolated Linux VM with a throwaway install, as the unprivileged dotnetcloud account attempt `mv /etc/dotnetcloud/config.json /etc/dotnetcloud/config.json.bak` and record whether the rename succeeds; if it does, place a fixture config.json with DataDirect...
- **Regression test:** CLI test: a config path outside the root-owned directory (or a symlink) is refused before any privileged write.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ client-token-store/predictable-machine-key-no-file-acl — needs validation

- **Title:** Desktop refresh tokens encrypted under a public machine-derived key with no owner-only file permission
- **Location:** `src/Clients/DotNetCloud.Client.SyncTray/App.axaml.cs:150` → `src/Clients/DotNetCloud.Client.Core/Auth/EncryptedFileTokenStore.cs:36`
- **Owner:** monolith
- **Unresolved blocker:** Effective ACL/permission of the created <dataDir>/<sha256>.tok file and its parent directories is an OS/runtime fact not visible in source: the Windows inherited ACL under %ProgramData%, the Linux umask, home-directory mode and any installer-created ACLs decide whether a second local account can read the ciphertext. Wi...
- **Before fixing:** On an isolated test host with two dummy local accounts, run the sync tray as account A with default permissions, then from account B attempt to read <dataDir>/<sha256>.tok and to decrypt it with SHA256("dotnetcloud:<hostname>:<A's username>") using only throwa...
- **Regression test:** Token-store test: the key file is DPAPI/Keychain-protected and owner-only; a second OS user cannot decrypt the refresh token.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ crypto/oidc-key-custody-windows-programdata-acl — needs validation

- **Title:** Windows install stores OIDC signing/encryption keys and install secrets under ProgramData with no ACL hardening
- **Location:** `tools/install-windows.ps1:50` → `tools/install-windows.ps1:601`
- **Owner:** mint22
- **Unresolved blocker:** The effective NTFS DACL inherited by %ProgramData%\DotNetCloud, and any GPO or vendor hardening of %ProgramData%, is host state not represented in the repository. Whether an unprivileged local principal exists on the host, and whether the operator installed with a custom -DataRoot on a restricted volume, are deployment...
- **Before fixing:** On a lab Windows host run tools/install-windows.ps1 with default parameters, let the service generate oidc-keys, then as a standard local user run icacls on %ProgramData%/DotNetCloud/oidc-keys/signing-key.pem and attempt Get-Content on that file, on config/dot...
- **Regression test:** Installer test: the OIDC key directory and config are readable only by the service identity and administrators.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ crypto/oidc-key-rotation-ordering-legacy-filename — needs validation

- **Title:** OIDC 'newest key' is chosen by a culture-sensitive filename sort that mixes the legacy undated key with dated rotation keys
- **Location:** `src/Core/DotNetCloud.Core.Auth/Services/OidcKeyRotationService.cs:87` → `src/Core/DotNetCloud.Core.Auth/Extensions/AuthServiceExtensions.cs:182`
- **Owner:** mint22
- **Unresolved blocker:** .NET's culture-sensitive ordering (Comparer<string>.Default over full paths) of 'signing-key.pem' versus 'signing-key-YYYY-MM-DD.pem' depends on the host culture/ICU collation and is not asserted anywhere in the repository. OpenIddict 7.7.2's choice of signing key among multiple credentials registered with AddSigningKe...
- **Before fixing:** Unit test in a temp directory containing signing-key.pem with an old mtime plus signing-key-2026-10-25.pem: assert GetNewestKeyFile/ShouldRotate/CleanupOldKeys results under en-US, de-DE and invariant cultures. Then start the server with both keys registered a...
- **Regression test:** Rotation test: with mixed legacy and dated filenames, the newest key is selected by parsed date, not culture-sensitive string order.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ files-upload-content-length-allocation — needs validation

- **Title:** Chunk upload allocates a buffer sized from the unvalidated Content-Length header before any body-size limit is applied
- **Location:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:333` → `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:337`
- **Owner:** mint22
- **Unresolved blocker:** Kestrel enforcement ordering is framework behaviour outside this repository: whether a declared Content-Length above MaxRequestBodySize is rejected before FilesController.UploadChunkAsync executes, or only when the body is first read after the allocation at line 337, cannot be established from repository source. The bo...
- **Before fixing:** On a disposable local machine, start only the Files module host with a Kestrel MaxRequestBodySize of 1 MB and one authenticated test session, then send one PUT /api/v1/files/upload/{sessionId}/chunks/{hash} with Content-Length 900000000 and a one-byte body. Re...
- **Regression test:** Owner-observed then test: an over-limit Content-Length is rejected before the action allocates, or the buffer is capped at a configured chunk size.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ files-zip-entry-name-traversal-on-download — needs validation

- **Title:** ZIP downloads pass unnormalized node names into archive entry paths (dot-dot folder names survive validation)
- **Location:** `src/Modules/Files/DotNetCloud.Modules.Files.Host/Controllers/FilesController.cs:483` → `src/Modules/Files/DotNetCloud.Modules.Files.Data/Services/FileService.cs:1723`
- **Owner:** mint22
- **Unresolved blocker:** No in-repo component extracts a Files-generated ZIP (the only ZipFile.ExtractToDirectory in src/Clients is the client update package), so the write-outside-destination effect depends on an external extractor in the desktop/mobile client or the user's OS. Without an extractor observation the impact is unproven; a caller...
- **Before fixing:** Statically confirm that ValidateFilenameCompatibility does not reject '..' or '.' and that DownloadService uses node.Name verbatim, then in a scratch directory outside the repository create an archive with entries '..' and '../probe.txt' and extract it with th...
- **Regression test:** Download test: a folder named `..` produces archive entries whose names contain no parent segment.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

#### ☐ supply-chain/installer-verify-then-extract-tmp-predictable — needs validation

- **Title:** Linux installer verifies a tarball at a predictable /tmp path and later extracts it with sudo
- **Location:** `tools/install.sh:513` → `tools/install.sh:564`
- **Owner:** mint22
- **Unresolved blocker:** The value of fs.protected_regular (and the /tmp mount options) on the target host decides whether the installer can truncate a pre-created, attacker-owned file and whether that attacker keeps write access after verification; neither is in the repository. Whether the installer is run as root directly (SUDO empty) or by ...
- **Before fixing:** In a disposable VM, as an unprivileged user pre-create /tmp/dotnetcloud-<version>-linux-x64.tar.gz owned by that user and writable (0666), then run the installer under sudo and overwrite the file with a bounded fixture archive containing only a marker file bet...
- **Regression test:** Installer test: extraction uses a root-owned private directory created exclusively; a pre-created path is rejected.
- **Done when:** the negative test above fails on current source and passes after the change, and the positive path still works.

## Phase P3 — re-audit after remediation

- ☐ Re-run the audit (`run-2`) scoped to the changed files plus the four residual gaps: Tracks service breadth, module hosts not covered by the first pass, anonymous chat upload breadth, CLI unit-file construction.
- ☐ Confirm each fixed fingerprint is either absent or re-classified, and that no new sibling path reintroduces the same root cause.
- ☐ Re-check the 9 needs-validation leads against the resolved blockers; promote, fix, or close each.
- ☐ Re-run the JSON validators on the new run (`Test-AuditJson.ps1 -Mode findings|ledger`).

## Explicitly not actioned

- `ci/release-workflow-write-token-mutable-actions` — rejected during verification: the trace starts at a workflow-level permission declaration with no lower-trust entrypoint, and the PR workflows run with `contents: read`. The concrete issue in that job (mutable appimagetool download) is tracked as its own item under Workstream 8.

## Method notes

- Fix text is copied from the verified `remediation.strategy` fields, not rewritten, so it stays aligned with `findings.json`.
- No source file was modified to produce this plan. Nothing is committed or pushed.
- The plan covers every finding record; low-severity items are included rather than dropped.
- The audit was source-only, so a passed regression test is the first real proof for any fix; do not mark an item ✓ on code inspection alone.

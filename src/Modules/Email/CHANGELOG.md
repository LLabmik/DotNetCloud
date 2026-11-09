# Changelog

All notable changes to the Email module are documented here.

## 1.2.0

Track C: the module leaves the core and runs as a sandboxed host with its own UI.

- **Data plane.** `EmailDbContext` and both provider migration sets are schema-neutral, and the
  host self-migrates into the schema the core provisions (`dataStore: "isolated"`). The model /
  snapshot drift that hid the JSON columns' `jsonb` mapping is repaired — the columns are mapped
  explicitly and SQL Server swaps them for `nvarchar(max)`.
- **Sandboxed host.** `module.json` topology, a module-private DataProtection ring under
  `DOTNETCLOUD_MODULE_DATA_DIR`, the scoped connection string, no cookie scheme, and
  `EmailExceptionFilter` turning a missing assertion into 401 instead of 500. Attachments live
  under the module data directory.
- **The user's files are a core capability.** `GET /api/v1/email/files`,
  `POST /api/v1/email/attachments/from-file` and
  `POST /api/v1/email/attachments/{id}/save-to-files` reach the user's tree through the core's
  brokered `IFileDirectory`/`IFileWriter` RPCs (scopes `files:read`/`files:write`). The direct
  module→module Files client and its `FilesGrpc:Address` setting are gone.
- **Module-hosted UI.** The module serves `wwwroot/index.html` + `assets/email-ui.js` (accounts,
  IMAP/Gmail setup, folders, threads, compose with uploads, the full rules editor, help) and
  `wwwroot/widget/` for the Home-page card. Message bodies render inside a nested `sandbox=""`
  frame, so injected handlers cannot run in the module's origin.
- **Gmail OAuth for the plane.** The CSRF state is a DataProtection-signed token bound to the
  asserted user (a cookie never round-trips — the proxy strips `Set-Cookie`), redirect targets are
  `/apps/dotnetcloud.email/…`, and an unconfigured server answers `{configured:false}`.
- **In-app consent.** A file operation without consent answers `FILES_CONSENT_REQUIRED` and the UI
  asks the core to prompt; approving the request retries the call.
- **Manifest.** Publisher `dotnetcloud`, `dataStore: "isolated"`, `handlesUserErasure: true`,
  capabilities narrowed to the two the code actually uses, four consent scopes, and a `ui` block
  with a sidebar entry and a widget.
- **Decoupled.** No `Core.Server`, `UI.Web` or CLI reference remains; the in-core pages, help
  component, Blazor components and widget project are deleted.

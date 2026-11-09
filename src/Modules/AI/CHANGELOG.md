# Changelog

All notable changes to the AI Assistant module are documented here.

## 2.0.0

Track C: the module leaves the core and runs as a sandboxed host with its own UI and widget.

- **Data plane.** `AiDbContext` and both provider migration sets are schema-neutral; the host
  self-migrates into the schema the core provisions (`dataStore: "isolated"`). Migrations keep their
  original ids, so an adopted install's history stays valid.
- **Sandboxed host.** `module.json` topology, a module-private DataProtection ring under
  `DOTNETCLOUD_MODULE_DATA_DIR`, the scoped connection string, no cookie scheme, and an exception
  filter that turns a missing assertion into 401 instead of 500. The host no longer opens the core
  database.
- **Settings moved to the module settings plane.** The provider, base URL, model, limit and API-key
  settings used to be core `SystemSettings` read through `IAdminSettingsService`. They now live in
  the core's module settings plane (`dotnetcloud.ai`), written from the module's admin page and read
  through `GetSetting`; the API key is encrypted at rest and never returned by a listing. The
  obsolete `dotnetcloud.ai` system-setting seeds were removed so there is one source of truth.
- **Module-hosted UI.** The module serves `wwwroot/index.html` + `assets/ai-ui.js` (streaming chat,
  conversation list with rename/delete, queue position, thought-process block, abort, copy as
  markdown/formatted, in-frame help) and `wwwroot/widget/` for the Home-page card. Model output is
  rendered by an escaping markdown renderer that only creates text nodes and a fixed element set.
- **New REST endpoint.** `GET api/v1/ai/conversations/stats` answers the widget using the asserted
  user, replacing the old gRPC shape that took a user id from the payload.
- **Least privilege.** The manifest declares only `IAuditLogger` and no consent scopes; the four
  capabilities and two events the old manifest claimed were never used and are gone.
- **Erasure.** `AiUserErasureHandler` deletes the user's conversations (including soft-deleted ones)
  and their messages, idempotently per request id.
- **Decoupled.** No `Core.Server`, `UI.Web` or CLI reference remains; the in-core chat page, admin
  settings component, help page and Blazor widget project are deleted, and the library is a plain
  class library.

## 1.x

- Core-hosted module: Blazor chat page inside `Core.Server`, core-managed `ai` schema, settings read
  from core `SystemSettings`, gRPC `AiService` used by the Blazor page and the Home widget.

## 1.0.0

- Initial release: chat conversations, streaming responses through Ollama, model selection display.

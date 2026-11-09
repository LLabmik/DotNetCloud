# DotNetCloud AI Assistant Module

Streaming LLM chat with conversation history, running as a process-isolated, sandboxed module with
its own web UI and Home-page widget.

## Overview

- **Own data store.** `dataStore: "isolated"` — the module self-migrates into the schema and
  principal the core provisions for it. `AiDbContext` and both provider migration sets are
  schema-neutral: the scoped connection string's `search_path` (PostgreSQL) or default schema
  (SQL Server) decides where `Conversations`, `ConversationMessages` and `__EFMigrationsHistory`
  land. Never name a schema in a sandboxed module's model or migrations.
- **Own UI.** `wwwroot/index.html` + `assets/ai-ui.js` are served by this host and framed by the
  core at `/apps/dotnetcloud.ai/`; `wwwroot/widget/` is the Home-page card. Every asset path is
  relative and every API call is same-origin to the frame. Model output is rendered by a small
  markdown renderer that only ever creates text nodes and a fixed element set, so a model cannot
  inject markup into the core's origin.
- **Identity.** There is no cookie scheme. The core proxy strips the browser's session and injects a
  `dnc-user-assertion`; the module answers 401 (never 500) when it is missing, and the acting user is
  always the asserted subject — never a user id from the request payload.
- **Administrator-configured provider.** The provider, base URL, model, token limits and API key live
  in the core's **module settings plane** (`dotnetcloud.ai` namespace), written by an administrator
  from the module's admin page and read through `GetSetting` on the module API. Secret values are
  encrypted at rest by the core and are never returned to a browser.
- **No capability over-request.** The module declares only `IAuditLogger` and `IModuleSettings` — audit,
  and the settings plane it reads its configuration through. Conversations never leave the module's
  schema, and the module publishes no cross-module events.
- **Erasure.** `AiUserErasureHandler` deletes the deleted user's conversations and messages,
  including conversations the user had soft-deleted.

## Manifest

| Field | Value |
| ----- | ----- |
| `id` | `dotnetcloud.ai` |
| `dataStore` | `isolated` |
| `capabilities` | `IAuditLogger`, `IModuleSettings` |
| `userConsentScopes` | none — the module reads and writes only its own data |
| `ui` | iframe app with a sidebar entry and a Home-page widget |

## Settings

| Key | Meaning | Secret |
| --- | ------- | ------ |
| `Provider` | `ollama`, `openai` or `anthropic` | no |
| `ApiBaseUrl` | Base URL of the LLM API | no |
| `ApiKey` | API key for a cloud provider (not needed for Ollama) | **yes** |
| `OrganizationId` | Optional organization id (OpenAI) | no |
| `DefaultModel` | Model new conversations use | no |
| `MaxTokens` | Maximum tokens per response (`0` = provider default) | no |
| `RequestTimeoutSeconds` | LLM request timeout | no |

Every key falls back to this host's `appsettings.json` (`AI:*`) so a standalone development host
still runs.

## Project structure

```
src/Modules/AI/
├── manifest.json                          # The manifest that ships (single source of truth)
├── DotNetCloud.Modules.AI/                # Domain contracts, events, module lifecycle
├── DotNetCloud.Modules.AI.Data/           # EF Core context, migrations (PostgreSQL), services
├── DotNetCloud.Modules.AI.Data.SqlServer/ # Migrations (SQL Server)
└── DotNetCloud.Modules.AI.Host/           # The sandboxed host: gRPC + REST + its own wwwroot
```

## Building and packaging

```bash
# Build
dotnet build src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj -c Release

# Package (signed with the publisher key)
dotnet src/Tools/DotNetCloud.ModulePackaging/bin/Release/net10.0/dotnetcloud-pack.dll pack \
  --project src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj \
  --rid linux-x64 --rid win-x64 \
  --sign-key ~/.keys/dotnetcloud-official-2026.key.json \
  --output /tmp/dotnetcloud.ai-2.0.0.dncpkg
```

The package must contain `manifest.json`, `LICENSE`, `README.md` and `CHANGELOG.md` beside it; the
packer copies whatever documents sit next to the manifest.

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).

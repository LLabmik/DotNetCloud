# AI Assistant Module — Admin Configuration Guide

> **Last Updated:** 2026-04-09  
> **Module ID:** `dotnetcloud.ai`

---

## Overview

The AI Assistant module provides LLM-powered chat, summarization, and content generation for all DotNetCloud users. It supports **local inference** via Ollama (no data leaves your server) and optional **cloud providers** (OpenAI, Anthropic) for organizations that prefer managed APIs.

The module runs as a separate process (`dotnetcloud-module ai`) and communicates with the core via gRPC, following the standard DotNetCloud module architecture.

---

## Module Registration

The AI module is discovered automatically via its `manifest.json`. No manual registration is required.

### Required Core Capabilities

| Capability | Purpose |
|---|---|
| `ICurrentUserContext` | Identify the authenticated user for per-user conversation isolation |
| `IAuditLogger` | Log AI operations for compliance and debugging |
| `IUserDirectory` | Resolve user information |
| `INotificationService` | Future: notify users of long-running completions |
| `ILlmProvider` | Core LLM abstraction for cross-module AI features |

If any required capability is unavailable, the module will not start. Check logs for capability resolution errors.

---

## LLM Provider Configuration

### Supported Providers

| Provider | Type | API Key Required | Use Case |
|---|---|---|---|
| **Ollama** | Local / LAN | No | Privacy-first; no data leaves your network |
| **OpenAI** | Cloud | Yes (`sk-...`) | GPT-4o, GPT-4o-mini, o3-mini |
| **Anthropic** | Cloud | Yes | Claude Sonnet, Haiku, Opus |

### Configuration via `appsettings.json`

Settings are located in the AI module's host configuration file:  
`src/Modules/AI/DotNetCloud.Modules.AI.Host/appsettings.json`

#### Ollama (Default — Recommended)

```json
{
  "AI": {
    "Provider": "ollama",
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "DefaultModel": "llama3:8b"
    }
  }
}
```

#### OpenAI

```json
{
  "AI": {
    "Provider": "openai",
    "ApiBaseUrl": "https://api.openai.com/",
    "ApiKey": "sk-your-api-key-here",
    "OrganizationId": "org-optional",
    "DefaultModel": "gpt-4o",
    "MaxTokens": 4096,
    "RequestTimeoutSeconds": 300
  }
}
```

#### Anthropic

```json
{
  "AI": {
    "Provider": "anthropic",
    "ApiBaseUrl": "https://api.anthropic.com/",
    "ApiKey": "sk-ant-your-api-key-here",
    "DefaultModel": "claude-sonnet-4-20250514",
    "MaxTokens": 4096,
    "RequestTimeoutSeconds": 300
  }
}
```

### Configuration Reference

| Setting | Default | Description |
|---|---|---|
| `AI:Provider` | `ollama` | LLM backend: `ollama`, `openai`, or `anthropic` |
| `AI:Ollama:BaseUrl` | `http://localhost:11434/` | URL of the Ollama instance (local or LAN) |
| `AI:ApiBaseUrl` | (per provider) | API endpoint for cloud providers. Change for compatible proxies. |
| `AI:ApiKey` | (empty) | API key for OpenAI or Anthropic. Not required for Ollama. Stored encrypted in the database when set via the admin UI. |
| `AI:OrganizationId` | (empty) | Optional OpenAI organization ID for billing |
| `AI:DefaultModel` | `gpt-oss:20b` | Default model for new conversations when the user doesn't specify one |
| `AI:MaxTokens` | `0` (provider default) | Max tokens per response. `0` uses the provider's default limit. |
| `AI:RequestTimeoutSeconds` | `300` (5 min) | HTTP timeout for LLM requests. Increase for large models or long prompts. |

### Settings Resolution Order

Settings are resolved with a fallback chain:

1. **Admin UI settings** — stored in the `SystemSetting` database table (module: `dotnetcloud.ai`)
2. **`appsettings.json`** — file-based configuration
3. **Built-in defaults** — safe fallback values

Admin UI settings always take precedence. This allows runtime provider switching without restarts.

---

## Admin Settings UI

Navigate to **Admin → AI Assistant** in the web dashboard. The settings page provides:

| Section | Controls |
|---|---|
| **LLM Provider** | Dropdown to select Ollama, OpenAI, or Anthropic |
| **API Endpoint** | Base URL input with provider-specific hints and placeholders |
| **Authentication** | API key (password field) and optional organization ID — only shown for cloud providers |
| **Model Configuration** | Default model name, max tokens per response |
| **Request Limits** | Request timeout in seconds |

Changes take effect immediately after clicking **Save Settings**. Use **Reset to Defaults** to restore built-in values.

---

## Ollama Setup

### Installing Ollama

Ollama is the recommended provider for self-hosted deployments. Install on the same server as DotNetCloud or on a dedicated GPU server on your LAN.

```bash
# Linux
curl -fsSL https://ollama.com/install.sh | sh

# Verify installation
ollama --version
```

### Pulling Models

```bash
# Recommended general-purpose models
ollama pull llama3:8b          # Fast, good for most tasks
ollama pull llama3:70b         # High quality, needs ≥48 GB VRAM
ollama pull mistral:7b         # Fast alternative
ollama pull phi3:14b           # Microsoft Phi-3

# List available models
ollama list
```

### Network Access

If Ollama runs on a separate machine, configure it to listen on all interfaces:

```bash
# /etc/systemd/system/ollama.service.d/override.conf
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
```

Then set `AI:Ollama:BaseUrl` to `http://<ollama-host>:11434/` in DotNetCloud.

### GPU Recommendations

| Model Size | Minimum VRAM | Recommended GPU |
|---|---|---|
| 7B–8B parameters | 8 GB | RTX 3070 / RTX 4060 Ti |
| 13B–14B parameters | 16 GB | RTX 4080 / A4000 |
| 30B–34B parameters | 24 GB | RTX 4090 / A5000 |
| 70B parameters | 48 GB+ | A6000 / 2× RTX 4090 |

CPU-only inference is supported but significantly slower. Ollama automatically uses GPU when available.

---

## Database

The AI module uses its own `AiDbContext` with a separate schema. No shared tables with other modules.

### Key Tables

| Table | Purpose |
|---|---|
| `Conversations` | Chat conversations: owner, title, model, system prompt, timestamps, soft-delete |
| `ConversationMessages` | Messages within conversations: role, content, token count, timestamp |

### Migrations

The AI module manages its own migrations:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Modules/AI/DotNetCloud.Modules.AI.Data \
  --context AiDbContext

dotnet ef database update --context AiDbContext
```

### Data Isolation

- Each conversation is owned by a single user (`OwnerId`)
- All queries are filtered by owner — users cannot see each other's conversations
- Deleted conversations are soft-deleted (`IsDeleted` flag) and can be recovered

---

## API Endpoints

The AI module exposes a REST API on port configured in the host. Base route: `/api/ai`

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/ai/conversations` | Create a new conversation |
| `GET` | `/api/ai/conversations` | List all conversations for the current user |
| `GET` | `/api/ai/conversations/{id}` | Get a conversation with its messages |
| `DELETE` | `/api/ai/conversations/{id}` | Soft-delete a conversation |
| `POST` | `/api/ai/conversations/{id}/messages` | Send a message (full response) |
| `POST` | `/api/ai/conversations/{id}/messages/stream` | Send a message (streaming SSE) |
| `GET` | `/api/ai/models` | List available models from the configured provider |
| `GET` | `/api/ai/health/ollama` | Health check for Ollama connectivity |
| `GET` | `/` | Module info (name, version, status) |

All endpoints require authentication. Conversation endpoints enforce per-user ownership.

---

## Cross-Module Integration

Other DotNetCloud modules can use AI capabilities via the `ILlmProvider` interface:

| Module | AI Use Case |
|---|---|
| **Notes** | Summarize, expand, translate, grammar check |
| **Chat** | Message summarization, smart replies |
| **Files** | Content summarization, document Q&A |
| **Email** | Draft replies, summarize threads |

These integrations use the same provider configuration — switching providers in the admin UI affects all modules.

---

## Events

The AI module publishes events that other modules can subscribe to:

| Event | Payload | Purpose |
|---|---|---|
| `ConversationCreatedEvent` | `ConversationId`, `OwnerId`, `Model` | Published when a new conversation is created |
| `ConversationMessageEvent` | `ConversationId`, `OwnerId`, `Role`, `TokenCount` | Published when a message is added |

---

## Health Monitoring

### Health Check Endpoint

```
GET /api/ai/health/ollama
```

Returns `{ "status": "healthy" }` or `{ "status": "unhealthy" }` based on Ollama connectivity.

### Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| "Ollama is not reachable" | Ollama service not running or wrong URL | Verify `ollama serve` is running; check `AI:Ollama:BaseUrl` |
| "Failed to load AI assistant" | Module can't connect to Ollama on startup | Check firewall rules; ensure Ollama listens on the configured interface |
| Slow responses | Model too large for available VRAM | Use a smaller model or add GPU resources |
| Timeout errors | `RequestTimeoutSeconds` too low | Increase timeout in admin settings; large models need longer |
| "No models available" | No models pulled in Ollama | Run `ollama pull <model-name>` on the Ollama host |
| Cloud API errors | Invalid API key or quota exceeded | Verify API key in admin settings; check provider dashboard for quota |

### Logs

AI module logs are written via Serilog with the `DotNetCloud.Modules.AI` source context. Check structured logs for:

- `Failed to initialize AI assistant page` — startup connectivity issues
- `Failed to create conversation` — database or auth errors
- `Ollama health check failed` — provider connectivity
- `Streaming error` — mid-response failures

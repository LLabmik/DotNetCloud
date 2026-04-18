# DotNetCloud AI Assistant Module

> **Module ID:** `dotnetcloud.ai`  
> **Version:** 1.0.0  
> **Status:** Implemented (Phase 4)  
> **License:** AGPL-3.0

---

## Overview

The AI Assistant module provides LLM-powered conversational AI within DotNetCloud. Users can have multi-turn chat conversations with large language models, with full conversation history and streaming responses. The module supports local inference via Ollama (privacy-first, no data leaves the network) and cloud providers (OpenAI, Anthropic).

## Key Features

| Feature | Description |
|---|---|
| **Multi-Turn Chat** | Persistent conversations with full message history |
| **Streaming Responses** | Token-by-token streaming via Server-Sent Events for real-time feedback |
| **Multiple Providers** | Ollama (local), OpenAI (cloud), Anthropic (cloud) |
| **Model Selection** | Per-conversation model choice from available models |
| **Conversation Management** | Create, rename (double-click), delete, list conversations |
| **Per-User Isolation** | All conversations are private — users only see their own chats |
| **Markdown Rendering** | Assistant responses rendered with full Markdown formatting |
| **Copy Support** | Copy responses as raw Markdown or rich formatted text |
| **System Prompts** | Optional per-conversation behavior customization via API |
| **Soft Delete** | Conversations are soft-deleted for recovery by administrators |
| **Token Counting** | Optional storage of token counts per message for usage tracking |
| **Admin Settings UI** | Web-based admin page for provider, model, and limit configuration |
| **Health Monitoring** | Built-in Ollama health check endpoint |
| **Cross-Module AI** | Other modules can use AI capabilities via `ILlmProvider` |

## Architecture

The AI module follows the standard DotNetCloud module architecture with three projects:

```
src/Modules/AI/
├── DotNetCloud.Modules.AI/            # Core SDK: models, interfaces, UI components, events
│   ├── Models/                        # Conversation, ConversationMessage entities
│   ├── Services/                      # IAiChatService, IOllamaClient, IAiSettingsProvider
│   ├── Events/                        # ConversationCreatedEvent, ConversationMessageEvent
│   └── UI/                            # Blazor: AiChatPage, AiAdminSettings
├── DotNetCloud.Modules.AI.Data/       # Implementation: EF Core, service implementations
│   ├── Configuration/                 # Entity type configurations
│   └── Services/                      # AiChatService, OllamaClient, AiSettingsProvider
├── DotNetCloud.Modules.AI.Host/       # REST host: controllers, health checks, Program.cs
│   ├── Controllers/                   # AiChatController (REST API)
│   └── Services/                      # AiHealthCheck, InProcessEventBus
└── manifest.json                      # Module declaration
```

### Process Isolation

The AI module runs as a separate process (`dotnetcloud-module ai`) and communicates with the DotNetCloud core via gRPC over Unix sockets or Named Pipes.

## Core Abstractions

### DTOs (`DotNetCloud.Core.AI`)

| Type | Purpose |
|---|---|
| `LlmMessage` | Single message in an LLM conversation (role + content) |
| `LlmRequest` | LLM API request: model, messages, streaming flag, temperature, max tokens, system prompt |
| `LlmResponse` | Full completion response with token counts and duration |
| `LlmResponseChunk` | Streaming chunk with partial content and done flag |
| `LlmModelInfo` | Available model metadata: ID, name, provider, size, parameter count |

### Capability Interface

| Interface | Tier | Purpose |
|---|---|---|
| `ILlmProvider` | Restricted | Core LLM abstraction: `CompleteAsync()`, `CompleteStreamingAsync()`, `ListModelsAsync()` |

Other modules request the `ILlmProvider` capability to use AI features without depending on the AI module directly.

## API Reference

Base route: `/api/ai`

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/ai/conversations` | Create a new conversation (title, model, system prompt) |
| `GET` | `/api/ai/conversations` | List all conversations for the authenticated user |
| `GET` | `/api/ai/conversations/{id}` | Get a conversation with all messages |
| `DELETE` | `/api/ai/conversations/{id}` | Soft-delete a conversation |
| `POST` | `/api/ai/conversations/{id}/messages` | Send a message and get a full response |
| `POST` | `/api/ai/conversations/{id}/messages/stream` | Send a message and get a streaming SSE response |
| `GET` | `/api/ai/models` | List available models from the configured provider |
| `GET` | `/api/ai/health/ollama` | Health check for Ollama connectivity |

### Streaming Response Format

The streaming endpoint uses Server-Sent Events:

```
data: {"content":"Hello","done":false,"evalCount":null}

data: {"content":" there","done":false,"evalCount":null}

data: {"content":"!","done":true,"evalCount":42}

data: [DONE]
```

## Events

| Event | Payload | Published When |
|---|---|---|
| `ConversationCreatedEvent` | `ConversationId`, `OwnerId`, `Model` | New conversation created |
| `ConversationMessageEvent` | `ConversationId`, `OwnerId`, `Role`, `TokenCount` | Message added to a conversation |

## Required Capabilities

| Capability | Usage |
|---|---|
| `ICurrentUserContext` | Identify the authenticated user |
| `IAuditLogger` | Audit AI operations |
| `IUserDirectory` | Resolve user information |
| `INotificationService` | Notify users of completions (future) |
| `ILlmProvider` | Core LLM abstraction |

## Cross-Module Integration

Modules can use the AI module's capabilities via the `ILlmProvider` interface:

| Module | Use Case |
|---|---|
| **Notes** | Summarize, expand, translate, grammar check |
| **Chat** | Message summarization, smart replies |
| **Files** | Content summarization, document Q&A |
| **Email** | Draft replies, summarize threads |

## Database

The AI module uses its own `AiDbContext` (separate from `CoreDbContext`).

| Table | Purpose |
|---|---|
| `Conversations` | Chat conversations: owner, title, model, system prompt, timestamps, soft-delete flag |
| `ConversationMessages` | Messages: role (system/user/assistant), content, token count, timestamp |

## Documentation

| Document | Audience | Path |
|---|---|---|
| Admin Configuration Guide | Administrators | [docs/admin/AI_ASSISTANT.md](../admin/AI_ASSISTANT.md) |
| User Guide | End users | [docs/user/AI_ASSISTANT.md](../user/AI_ASSISTANT.md) |
| Architecture | Developers | [docs/architecture/ARCHITECTURE.md](../architecture/ARCHITECTURE.md) (Section 8) |

## Tests

| Test File | Coverage |
|---|---|
| `AiModuleTests.cs` | Module lifecycle, manifest, event subscriptions |
| `OllamaClientTests.cs` | HTTP client: health check, chat requests, error handling |
| `AiChatServiceTests.cs` | Service: conversations, ownership isolation, message persistence |

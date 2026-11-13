# DotNetCloud.Modules.Email

> **Purpose:** Email module contracts — models, service interfaces, events, and the HTTP API client
> **Type:** Library (class library)
> **Target Framework:** net10.0

## Overview

The Email module's contract library. It holds the entity models, the service interfaces, the domain
events and the HTTP client for the module's own REST API. The module's **UI is not here**: since
Track C it is a static app served by `DotNetCloud.Modules.Email.Host/wwwroot` and framed by the core
at `/apps/dotnetcloud.email/`, so this project has no Razor SDK, no ASP.NET Core framework reference
and no dependency on the core's Blazor component library.

## Key Features

- **Models** — accounts, mailboxes, threads, messages, attachments, rules (conditions and actions)
- **Service contracts** — accounts, send, sync, rules, providers (`IEmailProvider`), attachment storage
- **Events** — account added/removed, thread created, message received, sent, rule triggered

> There is no module-side HTTP client: the host owns the REST surface (its UI is JavaScript) and the
> core reaches the module over gRPC through `IEmailApiClient` in `DotNetCloud.Core`.

## Projects This Interacts With

### Direct Dependencies

- `DotNetCloud.Core` — core interfaces and module contracts

### Dependent Projects

- `DotNetCloud.Modules.Email.Data` — EF Core model and services
- `DotNetCloud.Modules.Email.Host` — the process that runs the module

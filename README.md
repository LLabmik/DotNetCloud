<p align="center">
  <img src="assets/logo.png" alt="Dot — the DotNetCloud mascot" width="128" />
</p>

<h1 align="center">DotNetCloud</h1>

<p align="center"><em>Meet <strong>Dot</strong> — a smiling cloud with a net wrapped around her, and the mascot who completes the name <strong>Dot</strong>NetCloud.</em></p>

<p align="center"><strong>A self-hosted, open-source cloud platform built on .NET 10/C#.</strong></p>

DotNetCloud is a modern alternative to NextCloud and OwnCloud, designed to leverage .NET's async, multithreaded capabilities to provide a comprehensive, extensible cloud solution that anyone can install on their own server.

🌐 **Website:** [dotnetcloud.net](https://dotnetcloud.net) (Coming soon)

---

## What Is DotNetCloud?

DotNetCloud gives you full control of your data by running your own cloud server. Install it on your own hardware, your own domain, and never depend on a third-party cloud provider again.

### Features (Planned)

| Feature | Description | Status |
|---|---|---|
| 📁 **Files** | File sync and sharing with desktop client | ✅ Phase 1 |
| 📝 **Document Editing** | Browser-based editing via Collabora Online (LibreOffice-based) | ✅ Phase 1 |
| 💬 **Chat** | Real-time messaging, channels, direct messages | ✅ Phase 2 |
| 📱 **Android App** | Mobile client with photo auto-upload | ✅ Phase 2 |
| 👤 **Contacts** | Contact management with CardDAV support | Phase 3 |
| 📅 **Calendar** | Events, scheduling, CalDAV support | Phase 3 |
| 📝 **Notes** | Markdown-based note taking | Phase 3 |
| 📋 **Deck** | Kanban boards and project management | Phase 4 |
| 🖼️ **Photos** | Photo/video library and management | Phase 5 |
| 🎵 **Music** | Music player with equalizer and playlists | Phase 5 |
| 📧 **Email** | Integrated email client (SMTP/IMAP/Gmail) | Phase 6 |
| 🔖 **Bookmarks** | Browser bookmark sync via extension | Phase 6 |
| 📹 **Video Calls** | WebRTC video calling and screen sharing | Phase 7 |
| 🔍 **Search** | Full-text search across all modules | Phase 8 |
| 🔒 **E2EE** | Optional zero-knowledge encryption | Phase 8 |
| 🤖 **AI Assistant** | LLM-powered assistant via Ollama (local) or Claude/OpenAI (cloud) | Phase 9 |

### Roadmap

See [ROADMAP.md](ROADMAP.md) for the full phased implementation plan. Detailed status is tracked in [MASTER_PROJECT_PLAN.md](docs/MASTER_PROJECT_PLAN.md).

| Phase | Milestone | Status |
|---|---|---|
| **Phase 0** | Foundation — core platform, auth, module system, CLI, web shell, observability | ✅ Complete |
| **Phase 1** | Files + Desktop Sync Client — file browser, upload/download, sharing, Collabora editing, bulk operations, trash, tags, versioning, Windows & Linux sync | ✅ Complete |
| **Phase 2** | Chat + Notifications + Android — real-time messaging, push notifications, Android MAUI client | ✅ Complete |
| **Phase 3** | Contacts + Calendar + Notes + NextCloud Migration | ⬜ Planned |
| **Phase 4** | Project Management (Deck) | ⬜ Planned |
| **Phase 5** | Photos + Music | ⬜ Planned |
| **Phase 6** | Email + Bookmarks | ⬜ Planned |
| **Phase 7** | Video Calling + Screen Sharing | ⬜ Planned |
| **Phase 8** | Search + Auto-Updates + E2EE | ⬜ Planned |
| **Phase 9** | AI Assistant | ⬜ Planned |

---

## Why DotNetCloud?

- **Own your data** — Self-hosted on your server, your domain, your rules
- **Modern platform** — Built on .NET 10 with true async/multithreaded performance, overcoming the limitations PHP-based alternatives have hit
- **Extensible** — Third-party developers can build modules with the same power as first-party features using a well-documented plugin API
- **Cross-platform** — Server runs on Windows and Linux. Clients for Windows, Linux, and Android.
- **Multiple databases** — Supports PostgreSQL, SQL Server, and MariaDB via Entity Framework Core
- **Secure** — Process-isolated modules, granular permissions, OAuth2/OIDC, MFA (TOTP + FIDO2), optional zero-knowledge encryption
- **Easy to install** — One-command setup with interactive wizard

---

## Quick Start

### Linux (Debian/Ubuntu/Mint)

```sh
curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash
```

The installer now runs the beginner-friendly setup automatically on a fresh install.
If you need to run it again later, use:

```sh
sudo dotnetcloud setup --beginner
```

During beginner setup, DotNetCloud now asks one simple deployment question:

- private home/LAN or local test install: uses self-signed HTTPS on DotNetCloud directly
- public internet with a real domain name behind a reverse proxy: keeps DotNetCloud on local HTTP and tells you to put nginx, Apache, Caddy, or another reverse proxy with TLS in front of it
- public internet directly on the DotNetCloud server: lets DotNetCloud serve HTTPS itself when you already have a public certificate file, while still explaining why a reverse proxy is usually the better public setup

If you choose the reverse-proxy option and want a beginner-friendly walkthrough, see:

- [docs/admin/server/REVERSE_PROXY_BEGINNER_GUIDE.md](docs/admin/server/REVERSE_PROXY_BEGINNER_GUIDE.md)

### Windows

```powershell
winget install DotNetCloud
dotnetcloud setup --beginner
```

### Docker

```sh
dotnetcloud setup --docker
docker compose up -d
```

---

## Architecture

DotNetCloud uses a **modular monolith with process-isolated modules** architecture. Each module (Files, Chat, Calendar, etc.) runs in its own process, communicating with the core via gRPC over Unix sockets or Named Pipes. This provides:

- **Security** — Modules can't access each other's data or the core's sensitive data
- **Reliability** — A crashed module doesn't take down the whole system
- **Extensibility** — Third-party modules use the exact same architecture as first-party ones

```
dotnetcloud (core process — supervisor)
├── dotnetcloud-module files      (gRPC)
├── dotnetcloud-module chat       (gRPC)
├── dotnetcloud-module calendar   (gRPC)
└── livekit                       (optional, video calling)
```

For full details, see the [Architecture Document](docs/architecture/ARCHITECTURE.md).

---

## Platform Support

| Platform | Server | Desktop Client | Mobile Client |
|---|---|---|---|
| **Windows** | ✅ | ✅ (Avalonia) | — |
| **Linux (Debian-derived)** | ✅ | ✅ (Avalonia) | — |
| **Android** | — | — | ✅ (.NET MAUI) |
| **macOS** | 🔜 Future | 🔜 Future | — |
| **iOS** | — | — | 🔜 Future |

### Deployment Options

| Option | Reverse Proxy |
|---|---|
| Bare metal (built-in process supervisor) | IIS, Apache, nginx, or direct Kestrel |
| Docker Compose | Any |
| Kubernetes (Helm chart) | Any |

### Database Support

| Database | Status |
|---|---|
| PostgreSQL | ✅ Supported |
| SQL Server | ✅ Supported |
| MariaDB | ✅ Supported |
| Oracle | 🔜 Future |

---

## Desktop Sync Client

The sync client keeps your files synchronized between your devices and your DotNetCloud server.

- **Background sync service** — Runs as a Windows Service or systemd unit, syncs even when you're logged out
- **Tray app** — Shows sync status, folder selection, account settings
- **Multi-user & multi-account** — Multiple OS users on the same machine each get fully isolated sync. Each user can connect to multiple DotNetCloud servers (e.g., personal + work) with independent sync folders, credentials, and selective-sync settings
- **Chunked, resumable uploads** — Large files sync efficiently; interrupted transfers resume automatically
- **Conflict resolution** — Both versions are kept with a clear notification to resolve
- **Selective sync** — Choose which folders sync to which devices

---

## Building Modules

DotNetCloud is designed for extensibility. Third-party developers can build modules that integrate deeply with the platform — adding database tables, API endpoints, Blazor UI pages, background jobs, and event handlers.

```sh
# Scaffold a new module
dotnet new dotnetcloud-module -n MyCustomModule
```

See the [Module Development Guide](docs/module-development/) for the full walkthrough, including the [Example Module](src/Modules/DotNetCloud.Modules.Example/) as a working reference.

---

## Migrating from NextCloud

Planning to migrate? DotNetCloud will include a migration tool:

```sh
dotnetcloud migrate --from nextcloud --data-dir /var/www/nextcloud
```

Imports users, files, calendars, contacts, and bookmarks. Available starting in Phase 3.

---

## Technology Stack

| Component | Technology |
|---|---|
| Server | ASP.NET Core (.NET 10) |
| Web UI | Blazor |
| Desktop Client | Avalonia |
| Mobile Client | .NET MAUI (Android) |
| Database ORM | Entity Framework Core |
| Auth | ASP.NET Core Identity + OpenIddict (OAuth2/OIDC) |
| Real-time | SignalR |
| Module IPC | gRPC |
| Video (optional) | LiveKit (Apache 2.0) |
| Document Editing (optional) | Collabora CODE / Collabora Online (MPL-2.0) |
| Logging | Serilog |
| Telemetry | OpenTelemetry |

All dependencies are open source with permissive licenses. Zero cost. See the [full dependency list](docs/architecture/ARCHITECTURE.md#21-dependencies).

---

## Contributing

DotNetCloud is open source and welcomes contributions!

### Developer Quick Setup

For detailed, consistent commit messages (including AI-assisted workflows), set the repository commit template once in your clone:

```sh
git config commit.template .gitmessage
```

See `CONTRIBUTING.md` for full commit guidance and examples.

- **Repository:** [github.com/LLabmik/DotNetCloud](https://github.com/LLabmik/DotNetCloud) (primary)
- **License:** AGPL-3.0 (server & modules), Apache 2.0 (SDK/interfaces)
- **Module SDK:** Apache 2.0 — build proprietary or open-source modules

---

## License

- **Server & first-party modules:** [AGPL-3.0](LICENSE)
- **DotNetCloud.Core SDK (interfaces):** [Apache-2.0](src/Core/DotNetCloud.Core/LICENSE)
- **Documentation:** [CC BY-SA 4.0](docs/LICENSE)
- **Logo & mascot (Dot):** [CC BY-ND 4.0](assets/LICENSE) — © 2026 Ben P Kimball dba LLabmik Software Development

---

## Support

DotNetCloud is free and open source. Commercial support and services are available for individuals and organizations.

📧 Contact: [dotnetcloud.net](https://dotnetcloud.net)

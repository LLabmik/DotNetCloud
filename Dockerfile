# =============================================================================
# DotNetCloud Core Server — Multi-Stage Dockerfile
# =============================================================================
# Stage 1: restore  — Restore NuGet packages (cached layer)
# Stage 2: build    — Compile the solution
# Stage 3: publish  — Publish the server project
# Stage 4: runtime  — Minimal runtime image
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Restore
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore

# Disable all Microsoft telemetry in build stages
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

WORKDIR /src

# Copy solution and project files first for layer caching
COPY DotNetCloud.sln ./
COPY DotNetCloud.CI.slnf ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY global.json ./
COPY NuGet.config ./

# Copy all .csproj files preserving directory structure
# Core
COPY src/Core/DotNetCloud.Core/DotNetCloud.Core.csproj src/Core/DotNetCloud.Core/
COPY src/Core/DotNetCloud.Core.Auth/DotNetCloud.Core.Auth.csproj src/Core/DotNetCloud.Core.Auth/
COPY src/Core/DotNetCloud.Core.Data/DotNetCloud.Core.Data.csproj src/Core/DotNetCloud.Core.Data/
COPY src/Core/DotNetCloud.Core.Grpc/DotNetCloud.Core.Grpc.csproj src/Core/DotNetCloud.Core.Grpc/
COPY src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj src/Core/DotNetCloud.Core.Server/
COPY src/Core/DotNetCloud.Core.ServiceDefaults/DotNetCloud.Core.ServiceDefaults.csproj src/Core/DotNetCloud.Core.ServiceDefaults/
# UI
COPY src/UI/DotNetCloud.UI.Web/DotNetCloud.UI.Web.csproj src/UI/DotNetCloud.UI.Web/
COPY src/UI/DotNetCloud.UI.Web.Client/DotNetCloud.UI.Web.Client.csproj src/UI/DotNetCloud.UI.Web.Client/
COPY src/UI/DotNetCloud.UI.Shared/DotNetCloud.UI.Shared.csproj src/UI/DotNetCloud.UI.Shared/
# CLI
COPY src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj src/CLI/DotNetCloud.CLI/
# Modules — Example
COPY src/Modules/Example/DotNetCloud.Modules.Example/DotNetCloud.Modules.Example.csproj src/Modules/Example/DotNetCloud.Modules.Example/
COPY src/Modules/Example/DotNetCloud.Modules.Example.Data/DotNetCloud.Modules.Example.Data.csproj src/Modules/Example/DotNetCloud.Modules.Example.Data/
COPY src/Modules/Example/DotNetCloud.Modules.Example.Host/DotNetCloud.Modules.Example.Host.csproj src/Modules/Example/DotNetCloud.Modules.Example.Host/
# Modules — Files
COPY src/Modules/Files/DotNetCloud.Modules.Files/DotNetCloud.Modules.Files.csproj src/Modules/Files/DotNetCloud.Modules.Files/
COPY src/Modules/Files/DotNetCloud.Modules.Files.Data/DotNetCloud.Modules.Files.Data.csproj src/Modules/Files/DotNetCloud.Modules.Files.Data/
COPY src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj src/Modules/Files/DotNetCloud.Modules.Files.Host/
# Modules — Chat
COPY src/Modules/Chat/DotNetCloud.Modules.Chat/DotNetCloud.Modules.Chat.csproj src/Modules/Chat/DotNetCloud.Modules.Chat/
COPY src/Modules/Chat/DotNetCloud.Modules.Chat.Data/DotNetCloud.Modules.Chat.Data.csproj src/Modules/Chat/DotNetCloud.Modules.Chat.Data/
COPY src/Modules/Chat/DotNetCloud.Modules.Chat.Host/DotNetCloud.Modules.Chat.Host.csproj src/Modules/Chat/DotNetCloud.Modules.Chat.Host/
# Modules — Contacts
COPY src/Modules/Contacts/DotNetCloud.Modules.Contacts/DotNetCloud.Modules.Contacts.csproj src/Modules/Contacts/DotNetCloud.Modules.Contacts/
COPY src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data/DotNetCloud.Modules.Contacts.Data.csproj src/Modules/Contacts/DotNetCloud.Modules.Contacts.Data/
COPY src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/DotNetCloud.Modules.Contacts.Host.csproj src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/
# Modules — Calendar
COPY src/Modules/Calendar/DotNetCloud.Modules.Calendar/DotNetCloud.Modules.Calendar.csproj src/Modules/Calendar/DotNetCloud.Modules.Calendar/
COPY src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/DotNetCloud.Modules.Calendar.Data.csproj src/Modules/Calendar/DotNetCloud.Modules.Calendar.Data/
COPY src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/
# Modules — Notes
COPY src/Modules/Notes/DotNetCloud.Modules.Notes/DotNetCloud.Modules.Notes.csproj src/Modules/Notes/DotNetCloud.Modules.Notes/
COPY src/Modules/Notes/DotNetCloud.Modules.Notes.Data/DotNetCloud.Modules.Notes.Data.csproj src/Modules/Notes/DotNetCloud.Modules.Notes.Data/
COPY src/Modules/Notes/DotNetCloud.Modules.Notes.Host/DotNetCloud.Modules.Notes.Host.csproj src/Modules/Notes/DotNetCloud.Modules.Notes.Host/
# Clients (Android/MAUI excluded — requires workloads not in SDK Docker image)
COPY src/Clients/DotNetCloud.Client.Core/DotNetCloud.Client.Core.csproj src/Clients/DotNetCloud.Client.Core/
COPY src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj src/Clients/DotNetCloud.Client.SyncTray/

# Copy test projects (needed for solution restore; excluded from publish)
COPY tests/DotNetCloud.Core.Tests/DotNetCloud.Core.Tests.csproj tests/DotNetCloud.Core.Tests/
COPY tests/DotNetCloud.Core.Data.Tests/DotNetCloud.Core.Data.Tests.csproj tests/DotNetCloud.Core.Data.Tests/
COPY tests/DotNetCloud.Core.Auth.Tests/DotNetCloud.Core.Auth.Tests.csproj tests/DotNetCloud.Core.Auth.Tests/
COPY tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj tests/DotNetCloud.Core.Server.Tests/
COPY tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj tests/DotNetCloud.Integration.Tests/
COPY tests/DotNetCloud.CLI.Tests/DotNetCloud.CLI.Tests.csproj tests/DotNetCloud.CLI.Tests/
COPY tests/DotNetCloud.Modules.Example.Tests/DotNetCloud.Modules.Example.Tests.csproj tests/DotNetCloud.Modules.Example.Tests/
COPY tests/DotNetCloud.Modules.Files.Tests/DotNetCloud.Modules.Files.Tests.csproj tests/DotNetCloud.Modules.Files.Tests/
COPY tests/DotNetCloud.Modules.Chat.Tests/DotNetCloud.Modules.Chat.Tests.csproj tests/DotNetCloud.Modules.Chat.Tests/
COPY tests/DotNetCloud.Modules.Contacts.Tests/DotNetCloud.Modules.Contacts.Tests.csproj tests/DotNetCloud.Modules.Contacts.Tests/
COPY tests/DotNetCloud.Modules.Calendar.Tests/DotNetCloud.Modules.Calendar.Tests.csproj tests/DotNetCloud.Modules.Calendar.Tests/
COPY tests/DotNetCloud.Modules.Notes.Tests/DotNetCloud.Modules.Notes.Tests.csproj tests/DotNetCloud.Modules.Notes.Tests/
COPY tests/DotNetCloud.Client.Core.Tests/DotNetCloud.Client.Core.Tests.csproj tests/DotNetCloud.Client.Core.Tests/
COPY tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj tests/DotNetCloud.Client.SyncTray.Tests/

# Bring in the complete source tree so every current project (new modules,
# new test projects, etc.) is available to restore. .dockerignore keeps the
# layer lean by excluding bin/obj/artifacts/docs/.git and other heavy paths.
COPY . .

RUN dotnet restore DotNetCloud.CI.slnf

# ---------------------------------------------------------------------------
# Stage 2: Build
# ---------------------------------------------------------------------------
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY src/ src/
COPY tests/ tests/

# The .NET SDK can create build-output dirs with a literal backslash in the name on
# Linux (e.g. a directory literally named "bin\Debug" under a project, from the
# Roslyn Workspaces.MSBuild BuildHost content copy). MSBuild's glob expansion then
# chokes on them -> "MSB3552: Resource file "**/*.resx" cannot be found"
# (dotnet/msbuild#12546). Remove any such dirs before building.
RUN find . -type d -name '*\\*' -prune -exec rm -rf {} + ; \
    dotnet build DotNetCloud.CI.slnf --no-restore --configuration Release

# ---------------------------------------------------------------------------
# Stage 3: Publish
# ---------------------------------------------------------------------------
FROM build AS publish
WORKDIR /src

RUN dotnet publish src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj \
    --no-build \
    --configuration Release \
    --output /app/publish

# Publish every module host into /app/publish/modules/<module-id>/ so the core
# ProcessSupervisor can discover and spawn them at runtime. Module ID = the
# host csproj AssemblyName (e.g. "dotnetcloud.files"). Mirrors the module list
# and layout in .github/workflows/release.yml.
RUN set -eux; \
    for host_csproj in \
    src/Modules/Contacts/DotNetCloud.Modules.Contacts.Host/DotNetCloud.Modules.Contacts.Host.csproj \
    src/Modules/Calendar/DotNetCloud.Modules.Calendar.Host/DotNetCloud.Modules.Calendar.Host.csproj \
    src/Modules/Chat/DotNetCloud.Modules.Chat.Host/DotNetCloud.Modules.Chat.Host.csproj \
    src/Modules/Files/DotNetCloud.Modules.Files.Host/DotNetCloud.Modules.Files.Host.csproj \
    src/Modules/Notes/DotNetCloud.Modules.Notes.Host/DotNetCloud.Modules.Notes.Host.csproj \
    src/Modules/Tracks/DotNetCloud.Modules.Tracks.Host/DotNetCloud.Modules.Tracks.Host.csproj \
    src/Modules/Music/DotNetCloud.Modules.Music.Host/DotNetCloud.Modules.Music.Host.csproj \
    src/Modules/Photos/DotNetCloud.Modules.Photos.Host/DotNetCloud.Modules.Photos.Host.csproj \
    src/Modules/Video/DotNetCloud.Modules.Video.Host/DotNetCloud.Modules.Video.Host.csproj \
    src/Core/DotNetCloud.Core.Search.Extraction.Host/DotNetCloud.Core.Search.Extraction.Host.csproj \
    src/Modules/Bookmarks/DotNetCloud.Modules.Bookmarks.Host/DotNetCloud.Modules.Bookmarks.Host.csproj \
    src/Modules/Email/DotNetCloud.Modules.Email.Host/DotNetCloud.Modules.Email.Host.csproj \
    src/Modules/About/DotNetCloud.Modules.About.Host/DotNetCloud.Modules.About.Host.csproj \
    src/Modules/AI/DotNetCloud.Modules.AI.Host/DotNetCloud.Modules.AI.Host.csproj; do \
    module_name="$(sed -n 's:.*<AssemblyName>\([^<]*\)</AssemblyName>.*:\1:p' "$host_csproj" | head -n1)"; \
    echo "Publishing module host: ${module_name}"; \
    dotnet publish "$host_csproj" --no-build --configuration Release \
    --output "/app/publish/modules/${module_name}"; \
    done

# ---------------------------------------------------------------------------
# Stage 4: Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Native libraries required at runtime: Npgsql (PostgreSQL driver) loads
# libgssapi_krb5.so.2 for GSSAPI/SSPI support (module hosts crash without it),
# and openssl generates the self-signed TLS cert for the internal HTTPS loopback.
RUN apt-get update && \
    apt-get install -y --no-install-recommends libgssapi-krb5-2 openssl util-linux && \
    rm -rf /var/lib/apt/lists/*

# Copy the published server + module hosts first…
COPY --from=publish /app/publish .

# …then make the app tree writable by the unprivileged 'app' user (uid/gid 1654,
# shipped in the base image; uid/gid 1000 is taken by 'ubuntu'). Module hosts run
# from /app/modules/<id> and create runtime state (data-protection keys, etc.)
# in their own directories, so this MUST happen after the COPY above.
RUN mkdir -p /app/data /app/logs /app/modules /run/dotnetcloud && \
    chown -R app:app /app /run/dotnetcloud

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
# HTTPS-native: the app's internal loopback clients (realtime hub, SSR API,
# Contacts/Calendar pages) hardcode https://localhost:5443 and bypass cert
# validation, so Kestrel must serve HTTPS here. The entrypoint generates a
# self-signed cert into the persisted data volume on first start. HTTP on 8080
# is also served; public TLS is terminated by an upstream proxy/ingress.
ENV Kestrel__EnableHttps=true
ENV Kestrel__HttpPort=8080
ENV Kestrel__HttpsPort=5443
ENV Kestrel__CertificatePath=/app/data/certs/dotnetcloud-localhost.pfx
# appsettings.json ships a host-specific AllowedHosts list (localhost/mint22/…).
# Orchestrator health probes connect to the container/pod IP as Host, which would
# be rejected with HTTP 400. Allow all hosts; filtering happens at the edge.
ENV AllowedHosts=*
# Shared data dir: Core and every module host persist their ASP.NET Data
# Protection keys (and OIDC keys) under {DOTNETCLOUD_DATA_DIR}/data-protection-keys.
# Without this they each generate their own key ring, so module hosts cannot
# decrypt the auth cookie issued by Core ("Unprotect ticket failed").
ENV DOTNETCLOUD_DATA_DIR=/app/data
ENV DOTNET_EnableDiagnostics=0
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
# ProcessSupervisor: module hosts are discovered under /app/modules (default);
# keep any IPC sockets under /run/dotnetcloud.
ENV ProcessSupervisor__UnixSocketDirectory=/run/dotnetcloud

# The self-signed cert for the internal HTTPS loopback is generated at FIRST
# container start by the entrypoint into the persisted data volume
# ({DOTNETCLOUD_DATA_DIR}/certs) and trusted in the container CA store there.
# Baking it at build time regenerated it on every image rebuild, which kept
# invalidating the host's trust of the demo cert.

# Expose HTTP (proxy/health) and HTTPS (loopback + direct access) ports
EXPOSE 8080 5443

# Health check over HTTPS (self-signed => skip verification). wget is available
# in Debian-based aspnet images; curl is not.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --no-verbose --no-check-certificate --tries=1 --spider https://localhost:5443/health/live || exit 1

# Copy the entrypoint. It runs as root (the image default) to generate/trust the
# self-signed cert in the persisted data volume on first start, then drops to the
# unprivileged 'app' user (uid/gid 1654) to run the server.
COPY deploy/docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]

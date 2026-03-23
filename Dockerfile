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
COPY src/Clients/DotNetCloud.Client.SyncService/DotNetCloud.Client.SyncService.csproj src/Clients/DotNetCloud.Client.SyncService/
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
COPY tests/DotNetCloud.Client.SyncService.Tests/DotNetCloud.Client.SyncService.Tests.csproj tests/DotNetCloud.Client.SyncService.Tests/
COPY tests/DotNetCloud.Client.SyncTray.Tests/DotNetCloud.Client.SyncTray.Tests.csproj tests/DotNetCloud.Client.SyncTray.Tests/

RUN dotnet restore DotNetCloud.CI.slnf

# ---------------------------------------------------------------------------
# Stage 2: Build
# ---------------------------------------------------------------------------
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY src/ src/
COPY tests/ tests/

RUN dotnet build DotNetCloud.CI.slnf --no-restore --configuration Release

# ---------------------------------------------------------------------------
# Stage 3: Publish
# ---------------------------------------------------------------------------
FROM build AS publish
WORKDIR /src

RUN dotnet publish src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj \
    --no-build \
    --configuration Release \
    --output /app/publish

# ---------------------------------------------------------------------------
# Stage 4: Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd --gid 1000 dotnetcloud && \
    useradd --uid 1000 --gid dotnetcloud --shell /bin/bash --create-home dotnetcloud

# Create data directories
RUN mkdir -p /app/data /app/logs /app/modules && \
    chown -R dotnetcloud:dotnetcloud /app

COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

# Expose HTTP port
EXPOSE 8080

# Health check (wget is available in Debian-based aspnet images; curl is not)
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health/live || exit 1

# Switch to non-root user
USER dotnetcloud

ENTRYPOINT ["dotnet", "DotNetCloud.Core.Server.dll"]

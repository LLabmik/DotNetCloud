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
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY global.json ./
COPY NuGet.config ./

# Copy all .csproj files preserving directory structure
COPY src/Core/DotNetCloud.Core/DotNetCloud.Core.csproj src/Core/DotNetCloud.Core/
COPY src/Core/DotNetCloud.Core.Auth/DotNetCloud.Core.Auth.csproj src/Core/DotNetCloud.Core.Auth/
COPY src/Core/DotNetCloud.Core.Data/DotNetCloud.Core.Data.csproj src/Core/DotNetCloud.Core.Data/
COPY src/Core/DotNetCloud.Core.Grpc/DotNetCloud.Core.Grpc.csproj src/Core/DotNetCloud.Core.Grpc/
COPY src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj src/Core/DotNetCloud.Core.Server/
COPY src/Core/DotNetCloud.Core.ServiceDefaults/DotNetCloud.Core.ServiceDefaults.csproj src/Core/DotNetCloud.Core.ServiceDefaults/
COPY src/UI/DotNetCloud.UI.Web/DotNetCloud.UI.Web.csproj src/UI/DotNetCloud.UI.Web/
COPY src/UI/DotNetCloud.UI.Web.Client/DotNetCloud.UI.Web.Client.csproj src/UI/DotNetCloud.UI.Web.Client/
COPY src/UI/DotNetCloud.UI.Shared/DotNetCloud.UI.Shared.csproj src/UI/DotNetCloud.UI.Shared/
COPY src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj src/CLI/DotNetCloud.CLI/
COPY src/Modules/Example/DotNetCloud.Modules.Example/DotNetCloud.Modules.Example.csproj src/Modules/Example/DotNetCloud.Modules.Example/
COPY src/Modules/Example/DotNetCloud.Modules.Example.Data/DotNetCloud.Modules.Example.Data.csproj src/Modules/Example/DotNetCloud.Modules.Example.Data/
COPY src/Modules/Example/DotNetCloud.Modules.Example.Host/DotNetCloud.Modules.Example.Host.csproj src/Modules/Example/DotNetCloud.Modules.Example.Host/

# Copy test projects (needed for solution restore; excluded from publish)
COPY tests/DotNetCloud.Core.Tests/DotNetCloud.Core.Tests.csproj tests/DotNetCloud.Core.Tests/
COPY tests/DotNetCloud.Core.Data.Tests/DotNetCloud.Core.Data.Tests.csproj tests/DotNetCloud.Core.Data.Tests/
COPY tests/DotNetCloud.Core.Auth.Tests/DotNetCloud.Core.Auth.Tests.csproj tests/DotNetCloud.Core.Auth.Tests/
COPY tests/DotNetCloud.Core.Server.Tests/DotNetCloud.Core.Server.Tests.csproj tests/DotNetCloud.Core.Server.Tests/
COPY tests/DotNetCloud.Integration.Tests/DotNetCloud.Integration.Tests.csproj tests/DotNetCloud.Integration.Tests/
COPY tests/DotNetCloud.CLI.Tests/DotNetCloud.CLI.Tests.csproj tests/DotNetCloud.CLI.Tests/
COPY tests/DotNetCloud.Modules.Example.Tests/DotNetCloud.Modules.Example.Tests.csproj tests/DotNetCloud.Modules.Example.Tests/

RUN dotnet restore DotNetCloud.sln

# ---------------------------------------------------------------------------
# Stage 2: Build
# ---------------------------------------------------------------------------
FROM restore AS build
WORKDIR /src

# Copy all source code
COPY src/ src/
COPY tests/ tests/

RUN dotnet build DotNetCloud.sln --no-restore --configuration Release

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

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

# Switch to non-root user
USER dotnetcloud

ENTRYPOINT ["dotnet", "DotNetCloud.Core.Server.dll"]

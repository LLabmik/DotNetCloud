# Docker Setup Guide

> **Purpose:** Configure Docker for containerized local development and multi-database testing  
> **Audience:** Developers, DevOps engineers, QA  
> **Last Updated:** 2026-03-02

---

## Table of Contents

1. [Installation](#installation)
2. [Docker Compose Setup](#docker-compose-setup)
3. [Running Databases in Containers](#running-databases-in-containers)
4. [Application Container Setup](#application-container-setup)
5. [Local Development Workflow](#local-development-workflow)
6. [Multi-Database Testing](#multi-database-testing)
7. [Debugging Containers](#debugging-containers)
8. [Troubleshooting](#troubleshooting)

---

## Installation

### Windows

#### Option 1: Docker Desktop for Windows (Recommended)

1. **Download Docker Desktop**
   - Visit: [https://www.docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop)
   - Choose: Windows (AMD64 or ARM64)

2. **System Requirements**
   - Windows 11 Pro/Enterprise or Windows 10 Pro/Enterprise
   - Hyper-V enabled
   - 4GB+ RAM (8GB+ recommended)
   - WSL 2 backend (recommended over Hyper-V)

3. **Install Docker Desktop**
   - Run `Docker Desktop Installer.exe`
   - Follow installation wizard
   - Enable WSL 2 integration if prompted
   - Restart computer when prompted

4. **Verify Installation**
   ```powershell
   docker --version
   docker run hello-world
   ```

#### Option 2: Chocolatey

```powershell
choco install docker-desktop
```

### Linux (Ubuntu/Debian)

```bash
# Update package lists
sudo apt update

# Install Docker
sudo apt install -y docker.io docker-compose-plugin

# Add current user to docker group
sudo usermod -aG docker $USER

# Verify installation (logout and login first for group to take effect)
docker --version
docker run hello-world
```

### macOS

#### Using Docker Desktop

1. Download Docker Desktop from [https://www.docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop)
2. Choose: Mac (Intel or Apple Silicon)
3. Run the `.dmg` installer
4. Verify:
   ```bash
   docker --version
   docker run hello-world
   ```

#### Using Homebrew

```bash
brew install docker
brew install docker-compose
```

### Verify Docker Installation

```bash
# Check Docker daemon
docker info

# Check Docker Compose
docker compose version

# Run a test container
docker run --rm ubuntu echo "Docker is working!"
```

---

## Docker Compose Setup

### What is Docker Compose?

Docker Compose allows you to define and run multi-container applications. For DotNetCloud, we use it to run:
- PostgreSQL database
- SQL Server database
- MariaDB database
- Application (optional for local dev)

### Create `docker-compose.yml`

Create a file at the repository root: `docker-compose.yml`

```yaml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:16-alpine
    container_name: dotnetcloud-postgres
    environment:
      POSTGRES_USER: dotnetcloud
      POSTGRES_PASSWORD: postgres_dev_local_123
      POSTGRES_DB: dotnetcloud_dev
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U dotnetcloud -d dotnetcloud_dev"]
      interval: 5s
      timeout: 5s
      retries: 5

  # SQL Server Database
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: dotnetcloud-sqlserver
    environment:
      SA_PASSWORD: "SqlServer2022!Local"
      MSSQL_SA_PASSWORD: "SqlServer2022!Local"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SqlServer2022!Local' -Q 'SELECT 1' || exit 1"]
      interval: 5s
      timeout: 5s
      retries: 5

  # MariaDB Database
  mariadb:
    image: mariadb:11
    container_name: dotnetcloud-mariadb
    environment:
      MARIADB_ROOT_PASSWORD: mariadb_root_123
      MARIADB_USER: dotnetcloud
      MARIADB_PASSWORD: mariadb_dev_local_123
      MARIADB_DATABASE: dotnetcloud_dev
    ports:
      - "3306:3306"
    volumes:
      - mariadb_data:/var/lib/mysql
    healthcheck:
      test: ["CMD", "healthcheck.sh", "--su=dotnetcloud", "--connect", "--innodb_initialized"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  postgres_data:
  sqlserver_data:
  mariadb_data:
```

### Create Development Override File (Optional)

For developer-specific settings, create `docker-compose.override.yml`:

```yaml
version: '3.8'

services:
  postgres:
    # Expose to host machine only (security)
    ports:
      - "127.0.0.1:5432:5432"

  sqlserver:
    ports:
      - "127.0.0.1:1433:1433"

  mariadb:
    ports:
      - "127.0.0.1:3306:3306"
```

Note: Add `docker-compose.override.yml` to `.gitignore` if developer-specific.

---

## Running Databases in Containers

### Start All Services

```bash
# Start in background
docker compose up -d

# View status
docker compose ps

# View logs
docker compose logs -f

# View logs for specific service
docker compose logs -f postgres
```

### Start Specific Services

```bash
# Start only PostgreSQL
docker compose up -d postgres

# Start PostgreSQL and SQL Server
docker compose up -d postgres sqlserver
```

### Stop Services

```bash
# Stop all services
docker compose down

# Stop specific service
docker compose stop postgres

# Stop and remove volumes (WARNING: loses data!)
docker compose down -v
```

### Verify Database Connectivity

#### PostgreSQL

```bash
# Connect from command line
psql -h localhost -U dotnetcloud -d dotnetcloud_dev -c "SELECT version();"

# Or use Docker Compose exec
docker compose exec postgres psql -U dotnetcloud -d dotnetcloud_dev -c "SELECT version();"
```

#### SQL Server

```bash
# Using sqlcmd (if installed)
sqlcmd -S localhost -U sa -P "SqlServer2022!Local" -Q "SELECT @@version;"

# Using Docker container
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "SqlServer2022!Local" -Q "SELECT @@version;"
```

#### MariaDB

```bash
# Connect from command line
mysql -h localhost -u dotnetcloud -p -D dotnetcloud_dev -e "SELECT VERSION();"

# Or use Docker Compose exec
docker compose exec mariadb mysql -u dotnetcloud -p -D dotnetcloud_dev -e "SELECT VERSION();"
```

---

## Application Container Setup

### Create Dockerfile

Create a `Dockerfile` in the project root for the DotNetCloud API:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10 AS build
WORKDIR /build

# Copy solution and projects
COPY DotNetCloud.sln .
COPY src/ src/

# Restore dependencies
RUN dotnet restore

# Build application
RUN dotnet build -c Release --no-restore

# Publish stage
FROM mcr.microsoft.com/dotnet/aspnet:10 AS runtime
WORKDIR /app

# Copy published artifacts
COPY --from=build /build/src/Core/DotNetCloud.Core.Server/bin/Release/net10.0/publish .

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet /app/HealthCheckTool.dll || exit 1

# Run application
ENTRYPOINT ["dotnet", "DotNetCloud.Core.Server.dll"]
```

### Build Docker Image

```bash
# Build image locally
docker build -t dotnetcloud:latest .

# Build with specific tag
docker build -t dotnetcloud:dev-$(date +%s) .
```

### Run Application Container

Add to `docker-compose.yml`:

```yaml
  app:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: dotnetcloud-app
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ConnectionStrings__PostgreSQL: "Server=postgres;Port=5432;Database=dotnetcloud_dev;User Id=dotnetcloud;Password=postgres_dev_local_123;"
      Database__Provider: "PostgreSQL"
      ASPNETCORE_ENVIRONMENT: "Development"
      ASPNETCORE_URLS: "http://+:5000"
    ports:
      - "5000:5000"
    volumes:
      - ./src:/app/src  # Hot reload (if supported)
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 10s
      timeout: 5s
      retries: 3
```

### Run Full Stack

```bash
# Start all services (databases + app)
docker compose up -d

# Verify all services
docker compose ps

# View application logs
docker compose logs -f app
```

---

## Local Development Workflow

### Scenario 1: Databases in Docker, App Running Locally

This is the recommended setup for development:

```bash
# Terminal 1: Start databases only
docker compose up -d postgres  # or sqlserver/mariadb

# Verify health
docker compose ps

# Terminal 2: Run application locally
cd src/Core/DotNetCloud.Core.Server
dotnet run

# Application connects to Docker database
# Connection string: Server=localhost;Port=5432;Database=dotnetcloud_dev;...
```

### Scenario 2: Everything in Docker

```bash
# Terminal 1: Start full stack
docker compose up

# View logs
docker compose logs -f app
```

### Scenario 3: Run Tests Against Docker Databases

```bash
# Ensure databases are running
docker compose up -d postgres sqlserver mariadb

# Run tests against PostgreSQL
dotnet test -- --Database:Provider=PostgreSQL

# Run tests against SQL Server
dotnet test -- --Database:Provider=SqlServer

# Run tests against MariaDB
dotnet test -- --Database:Provider=MariaDb

# Stop databases
docker compose down
```

---

## Multi-Database Testing

### Full Matrix Testing

Create a test matrix to verify all three databases:

#### CI/CD Pipeline Example (`.github/workflows/test.yml`)

```yaml
name: Multi-Database Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        database: [PostgreSQL, SqlServer, MariaDb]
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: dotnetcloud
          POSTGRES_PASSWORD: postgres_dev_local_123
          POSTGRES_DB: dotnetcloud_dev
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          SA_PASSWORD: 'SqlServer2022!Local'
          ACCEPT_EULA: 'Y'
        options: >-
          --health-cmd "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SqlServer2022!Local' -Q 'SELECT 1'"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 1433:1433

      mariadb:
        image: mariadb:11
        env:
          MARIADB_ROOT_PASSWORD: mariadb_root_123
          MARIADB_USER: dotnetcloud
          MARIADB_PASSWORD: mariadb_dev_local_123
          MARIADB_DATABASE: dotnetcloud_dev
        options: >-
          --health-cmd "healthcheck.sh --su=dotnetcloud --connect --innodb_initialized"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 3306:3306

    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build -c Release --no-restore

      - name: Test (${{ matrix.database }})
        run: dotnet test -c Release --no-build -- --Database:Provider=${{ matrix.database }}
```

### Local Matrix Testing

```bash
#!/bin/bash
# test-all-databases.sh

echo "Starting databases..."
docker compose up -d postgres sqlserver mariadb

# Wait for health checks
sleep 10

echo "Running tests against PostgreSQL..."
dotnet test -- --Database:Provider=PostgreSQL

echo "Running tests against SQL Server..."
dotnet test -- --Database:Provider=SqlServer

echo "Running tests against MariaDB..."
dotnet test -- --Database:Provider=MariaDb

echo "Stopping databases..."
docker compose down

echo "All tests completed!"
```

Run:
```bash
chmod +x test-all-databases.sh
./test-all-databases.sh
```

---

## Debugging Containers

### View Container Logs

```bash
# Real-time logs
docker compose logs -f postgres

# Last 50 lines
docker compose logs --tail=50 postgres

# Logs with timestamps
docker compose logs --timestamps postgres
```

### Execute Commands in Container

```bash
# Interactive shell in PostgreSQL
docker compose exec postgres bash

# Run command in PostgreSQL
docker compose exec postgres psql -U dotnetcloud -d dotnetcloud_dev -c "SELECT * FROM information_schema.tables;"

# Interactive PowerShell in SQL Server
docker compose exec sqlserver powershell

# Run command in MariaDB
docker compose exec mariadb mysql -u dotnetcloud -p -D dotnetcloud_dev -e "SHOW TABLES;"
```

### Inspect Container

```bash
# View container metadata
docker compose ps postgres

# View detailed container info
docker inspect dotnetcloud-postgres

# View volume information
docker volume ls
docker volume inspect dotnetcloud_postgres_data
```

### Troubleshooting Container Issues

```bash
# Check health status
docker compose ps  # Look at STATUS column

# View events (real-time)
docker events --filter "container=dotnetcloud-postgres"

# View resource usage
docker stats dotnetcloud-postgres

# Check container logs for errors
docker compose logs postgres | grep -i error
```

---

## Troubleshooting

### Containers Won't Start

#### Check Docker Daemon
```bash
# Verify Docker is running
docker ps

# If not running:
# Windows: Start Docker Desktop
# Linux: sudo systemctl start docker
# macOS: open /Applications/Docker.app
```

#### View Error Logs
```bash
docker compose logs postgres  # View specific service logs
docker compose logs          # View all logs
```

#### Port Already in Use
```bash
# Find process using port
# Windows: netstat -ano | findstr :5432
# Linux/macOS: lsof -i :5432

# Solution: Change port in docker-compose.yml or stop conflicting container
docker compose down
```

### Database Connection Errors

#### Connection Refused (PostgreSQL)
```bash
# Verify container is running
docker compose ps

# Check health status
docker compose ps postgres

# Test connection
docker compose exec postgres psql -U dotnetcloud -d dotnetcloud_dev -c "SELECT 1;"
```

#### SQL Server Authentication Failed
```bash
# Verify password matches docker-compose.yml
# SQL Server requires strong passwords (uppercase, lowercase, number, special char)

# Test connection
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourPassword" -Q "SELECT 1;"
```

#### MariaDB Connection Issues
```bash
# Verify credentials
docker compose exec mariadb mysql -u dotnetcloud -p -D dotnetcloud_dev -e "SELECT 1;"

# Check MariaDB logs
docker compose logs mariadb
```

### Volume and Data Issues

#### Volumes Taking Up Space
```bash
# List volumes
docker volume ls

# Remove unused volumes
docker volume prune

# Remove specific volume (WARNING: destroys data)
docker volume rm dotnetcloud_postgres_data
```

#### Data Not Persisting After Restart
```bash
# Ensure volumes are defined in docker-compose.yml
# Check volume mount paths are correct

# View volume mount in running container
docker inspect dotnetcloud-postgres | grep -A 5 Mounts
```

### Performance Issues

#### Slow Database Operations
- Docker on Windows/macOS uses virtualization with performance overhead
- Consider running databases natively on Windows (SQL Server) or Linux VM
- Increase Docker resources: Docker Desktop → Settings → Resources

#### High CPU/Memory Usage
```bash
# Monitor resource usage
docker stats

# Limit container resources
# Add to docker-compose.yml under service:
# deploy:
#   resources:
#     limits:
#       cpus: '1'
#       memory: 2G
```

### Cleanup & Reset

```bash
# Stop all services
docker compose down

# Remove all containers, networks (keeps volumes)
docker compose down

# Remove everything including volumes (WARNING: loses data!)
docker compose down -v

# Remove unused containers, images, networks
docker system prune

# Full cleanup (keeps volumes)
docker system prune -a
```

---

## Best Practices

### Security

1. **Don't commit credentials** to version control
   - Use `.env` file (add to `.gitignore`)
   - Use Docker secrets for production

2. **Strong passwords** for database services
   - PostgreSQL: 12+ characters, mixed case, numbers
   - SQL Server: 8+ characters, uppercase, lowercase, number, special char
   - MariaDB: 12+ characters, mixed case, numbers

3. **Network isolation** in production
   - Use custom Docker networks instead of `host`
   - Bind to localhost only in development

### Performance

1. **Use Alpine images** for smaller footprints
   - `postgres:16-alpine` instead of `postgres:16`

2. **Multi-stage builds** in Dockerfile
   - Reduce image size

3. **Volume mounts** for better performance
   - Bind mounts for development code
   - Named volumes for database data

### Development Workflow

1. **Use `.env` file** for configuration
   ```bash
   # .env
   DATABASE_PASSWORD=mypassword
   DATABASE_USER=myuser
   ```

2. **Share docker-compose.yml** but not `docker-compose.override.yml`
   - Base configuration in `docker-compose.yml`
   - Developer overrides in `docker-compose.override.yml` (gitignored)

3. **Document container setup** in README
   - Quick start instructions
   - Port mappings
   - Expected health checks

---

## Next Steps

- Refer to [IDE_SETUP.md](./IDE_SETUP.md) for IDE Docker integration
- See [DATABASE_SETUP.md](./DATABASE_SETUP.md) for native database setup
- Review [DEVELOPMENT_WORKFLOW.md](./DEVELOPMENT_WORKFLOW.md) for team collaboration

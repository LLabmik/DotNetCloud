# DotNetCloud Server — Installation Guide

> **Last Updated:** 2026-03-07  
> **Applies To:** DotNetCloud 1.0.x  
> **Audience:** System administrators, self-hosters

---

## Table of Contents

1. [Overview](#overview)
2. [System Requirements](#system-requirements)
3. [Installation on Linux](#installation-on-linux)
4. [Installation on Windows](#installation-on-windows)
5. [Docker Installation](#docker-installation)
6. [Initial Setup Wizard](#initial-setup-wizard)
7. [Reverse Proxy Configuration](#reverse-proxy-configuration)
8. [TLS / Let's Encrypt](#tls--lets-encrypt)
9. [Verifying the Installation](#verifying-the-installation)
10. [Next Steps](#next-steps)

---

## Overview

DotNetCloud is a self-hosted cloud platform built on .NET 10. It runs as a Kestrel web server and is designed to be deployed behind a reverse proxy (nginx, Apache, or IIS) in production.

### Components

| Component | Description |
|---|---|
| **Core Server** | ASP.NET Core web server — REST API, SignalR, Blazor UI, module management |
| **CLI Tool** | `dotnetcloud` command-line interface for administration |
| **Module Hosts** | Separate processes for each module (Files, Chat, etc.) |
| **Database** | PostgreSQL (recommended), SQL Server, or MariaDB |

### Deployment Architecture

```
[Browser/Client]
       │
       ▼
[Reverse Proxy]  ←  nginx / Apache / IIS
       │
       ▼
[Core Server]    ←  Kestrel (HTTP :5080, HTTPS :5443)
       │
       ├── [Files Module Host]    ← gRPC / Unix socket
       ├── [Chat Module Host]     ← gRPC / Unix socket
       └── [Example Module Host]  ← gRPC / Unix socket
       │
       ▼
[Database]       ←  PostgreSQL / SQL Server / MariaDB
```

---

## System Requirements

### Minimum

| Resource | Requirement |
|---|---|
| **CPU** | 2 cores (x64 or ARM64) |
| **RAM** | 2 GB |
| **Disk** | 10 GB (OS + application) + storage for user files |
| **OS** | Ubuntu 22.04+, Debian 12+, RHEL 9+, Windows Server 2019+, Windows 10/11 |
| **.NET** | .NET 10 Runtime (installed automatically by packages) |
| **Database** | PostgreSQL 14+, SQL Server 2019+, or MariaDB 10.6+ |

### Recommended (50–100 users)

| Resource | Recommendation |
|---|---|
| **CPU** | 4+ cores |
| **RAM** | 4–8 GB |
| **Disk** | SSD, 50+ GB for file storage |
| **Database** | PostgreSQL 16 on dedicated instance or same host |

### Network

- Port 80 (HTTP) and 443 (HTTPS) open for reverse proxy
- Port 5080/5443 open only on localhost (Kestrel)
- Outbound HTTPS for Let's Encrypt (optional), Collabora, push notifications

---

## Installation on Linux

### Option A: One-Line Install (Ubuntu/Debian)

```bash
curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | sudo bash
```

This script:
1. Installs the .NET 10 runtime
2. Downloads the latest DotNetCloud release
3. Creates the `dotnetcloud` system user and group
4. Installs files to `/opt/dotnetcloud/`
5. Creates the systemd service unit
6. Runs the interactive setup wizard

### Option B: Manual Install (Ubuntu/Debian)

#### Step 1: Install Prerequisites

```bash
# Update package lists
sudo apt update && sudo apt upgrade -y

# Install .NET 10 runtime
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
sudo ./dotnet-install.sh --channel 10.0 --runtime aspnetcore --install-dir /usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet

# Verify .NET installation
dotnet --info
```

#### Step 2: Install PostgreSQL (Recommended)

```bash
# Install PostgreSQL 16
sudo apt install -y postgresql-16

# Start and enable the service
sudo systemctl enable postgresql
sudo systemctl start postgresql

# Create the DotNetCloud database and user
sudo -u postgres psql <<EOF
CREATE USER dotnetcloud WITH PASSWORD 'your-secure-password-here';
CREATE DATABASE dotnetcloud OWNER dotnetcloud;
GRANT ALL PRIVILEGES ON DATABASE dotnetcloud TO dotnetcloud;
EOF
```

**Alternative: SQL Server on Linux**

```bash
# Follow Microsoft's guide for your distro:
# https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-ubuntu

# After installation, create the database:
sqlcmd -S localhost -U sa -P 'YourStrong@Passw0rd' -Q "CREATE DATABASE dotnetcloud;"
```

#### Step 3: Create System User

```bash
# Create a dedicated user with no login shell
sudo useradd --system --no-create-home --shell /usr/sbin/nologin dotnetcloud

# Create directories
sudo mkdir -p /opt/dotnetcloud
sudo mkdir -p /var/lib/dotnetcloud/files
sudo mkdir -p /var/log/dotnetcloud
sudo mkdir -p /etc/dotnetcloud

# Set ownership
sudo chown -R dotnetcloud:dotnetcloud /opt/dotnetcloud
sudo chown -R dotnetcloud:dotnetcloud /var/lib/dotnetcloud
sudo chown -R dotnetcloud:dotnetcloud /var/log/dotnetcloud
sudo chown -R dotnetcloud:dotnetcloud /etc/dotnetcloud
```

#### Step 4: Download and Extract

```bash
# Download the latest release (replace VERSION with actual version)
VERSION="1.0.0"
wget "https://github.com/LLabmik/DotNetCloud/releases/download/v${VERSION}/dotnetcloud-${VERSION}-linux-x64.tar.gz" \
  -O /tmp/dotnetcloud.tar.gz

# Extract to /opt/dotnetcloud
sudo tar -xzf /tmp/dotnetcloud.tar.gz -C /opt/dotnetcloud --strip-components=1
sudo chown -R dotnetcloud:dotnetcloud /opt/dotnetcloud
```

#### Step 5: Build from Source (Alternative)

```bash
# Clone the repository
git clone https://github.com/LLabmik/DotNetCloud.git
cd DotNetCloud

# Publish the Core Server
dotnet publish src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output /opt/dotnetcloud/server

# Publish the CLI
dotnet publish src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output /opt/dotnetcloud/cli

# Symlink the CLI
sudo ln -sf /opt/dotnetcloud/cli/dotnetcloud /usr/local/bin/dotnetcloud

# Set ownership
sudo chown -R dotnetcloud:dotnetcloud /opt/dotnetcloud
```

#### Step 6: Create systemd Service

Create `/etc/systemd/system/dotnetcloud.service`:

```ini
[Unit]
Description=DotNetCloud Core Server
Documentation=https://github.com/LLabmik/DotNetCloud
After=network.target postgresql.service
Requires=network.target

[Service]
Type=notify
User=dotnetcloud
Group=dotnetcloud
WorkingDirectory=/opt/dotnetcloud/server
ExecStart=/opt/dotnetcloud/server/DotNetCloud.Core.Server
ExecStop=/bin/kill -SIGTERM $MAINPID

# Environment
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5080
Environment=ConnectionStrings__DefaultConnection=Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=your-secure-password-here

# File storage
Environment=Files__StorageRoot=/var/lib/dotnetcloud/files

# Logging
Environment=Serilog__FilePath=/var/log/dotnetcloud/dotnetcloud-.log

# Security
NoNewPrivileges=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/lib/dotnetcloud /var/log/dotnetcloud /etc/dotnetcloud
PrivateTmp=yes
PrivateDevices=yes

# Resource limits
LimitNOFILE=65535
MemoryMax=2G

# Restart policy
Restart=on-failure
RestartSec=5
StartLimitBurst=5
StartLimitIntervalSec=60

[Install]
WantedBy=multi-user.target
```

Enable and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable dotnetcloud
sudo systemctl start dotnetcloud

# Check status
sudo systemctl status dotnetcloud

# View logs
sudo journalctl -u dotnetcloud -f
```

#### Step 7: Run Initial Setup

```bash
sudo -u dotnetcloud dotnetcloud setup \
  --db-provider postgresql \
  --connection-string "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=your-secure-password-here"
```

Or run interactively:

```bash
sudo -u dotnetcloud dotnetcloud setup
```

The wizard will prompt for:
- Database provider and connection string
- Admin username and password
- Admin MFA setup (TOTP)
- Organization name
- TLS configuration
- Module selection

#### Step 8: Configure Reverse Proxy

See [Reverse Proxy Configuration](#reverse-proxy-configuration) below.

---

### Manual Install (RHEL/CentOS/Fedora)

The steps are the same as Ubuntu/Debian with these differences:

```bash
# Install .NET 10
sudo dnf install dotnet-runtime-10.0 aspnetcore-runtime-10.0

# Install PostgreSQL
sudo dnf install postgresql16-server postgresql16
sudo postgresql-setup --initdb
sudo systemctl enable postgresql
sudo systemctl start postgresql

# Firewall
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload

# SELinux (if enforcing)
sudo setsebool -P httpd_can_network_connect 1
```

---

### Directory Layout (Linux)

After installation, the directory layout follows FHS conventions:

```
/opt/dotnetcloud/
├── server/                 # Core Server binaries
│   ├── DotNetCloud.Core.Server
│   ├── appsettings.json    # Default configuration (do NOT edit)
│   └── wwwroot/            # Static web assets
├── cli/                    # CLI tool binaries
│   └── dotnetcloud
└── modules/                # Module host binaries
    ├── files/
    ├── chat/
    └── example/

/etc/dotnetcloud/
├── appsettings.Production.json   # Your configuration overrides
└── dotnetcloud.conf              # CLI configuration

/var/lib/dotnetcloud/
├── files/                  # File storage root
│   ├── ab/                 # Content-addressable chunks
│   └── .thumbnails/        # Thumbnail cache
└── data/                   # SQLite databases (sync state, etc.)

/var/log/dotnetcloud/
├── dotnetcloud-20260303.log
└── dotnetcloud-20260302.log
```

---

## Installation on Windows

### Option A: One-Command Install with IIS (Recommended)

The Windows installer script handles everything: .NET runtime verification, PostgreSQL installation, database creation, admin account setup, IIS configuration, HTTPS, and Windows Service registration.

Open an elevated PowerShell window and run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\tools\install-windows.ps1 -SourcePath .\artifacts\publish
```

You will be asked 3 questions: admin email, password, and confirm password. Everything else is automatic.

The script:
1. Verifies ASP.NET Core 10.0 runtime
2. Enables IIS and required Windows features
3. Installs URL Rewrite + ARR via winget
4. Installs PostgreSQL 17 via winget (if not present)
5. Creates the `dotnetcloud` database and user with a random password
6. Prompts for admin email and password
7. Copies binaries, writes config
8. Creates and starts the DotNetCloud Windows Service
9. Configures IIS reverse proxy to `http://localhost:5080`
10. Generates a self-signed HTTPS certificate on port 443
11. Opens firewall ports 80/443

After completion, open `https://localhost/` and log in.

**Optional flags:**
- `-SkipDatabaseInstall` — skip PostgreSQL auto-install (bring your own DB)
- `-SkipHttps` — skip self-signed certificate + HTTPS binding
- `-Advanced` — use the full CLI setup wizard instead of the simplified prompts
- `-HostName cloud.example.com` — set a specific hostname for the IIS site
- `-SkipFirewall` — skip Windows Firewall configuration

For detailed IIS-specific guidance, see [WINDOWS_IIS_INSTALL_GUIDE.md](WINDOWS_IIS_INSTALL_GUIDE.md).

### Option B: Manual Install

#### Step 1: Install Prerequisites

1. **Install .NET 10 Runtime**

   Download and install the [ASP.NET Core Runtime 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) (Hosting Bundle for IIS, or standalone runtime for direct Kestrel).

   Verify:

   ```powershell
   dotnet --info
   ```

2. **Install PostgreSQL** (Recommended)

   Download from [postgresql.org](https://www.postgresql.org/download/windows/) and install.

   During installation:
   - Set a password for the `postgres` superuser
   - Keep the default port (5432)

   After installation, create the DotNetCloud database:

   ```powershell
   & "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "CREATE USER dotnetcloud WITH PASSWORD 'your-secure-password-here';"
   & "C:\Program Files\PostgreSQL\16\bin\psql.exe" -U postgres -c "CREATE DATABASE dotnetcloud OWNER dotnetcloud;"
   ```

   **Alternative: SQL Server**

   If you already have SQL Server or SQL Server Express installed:

   ```powershell
   sqlcmd -S localhost -E -Q "CREATE DATABASE dotnetcloud;"
   ```

   Use Windows Authentication or create a SQL login:

   ```sql
   CREATE LOGIN dotnetcloud WITH PASSWORD = 'your-secure-password-here';
   USE dotnetcloud;
   CREATE USER dotnetcloud FOR LOGIN dotnetcloud;
   ALTER ROLE db_owner ADD MEMBER dotnetcloud;
   ```

#### Step 2: Download and Extract

```powershell
# Create installation directory
New-Item -ItemType Directory -Path "C:\DotNetCloud" -Force
New-Item -ItemType Directory -Path "C:\DotNetCloud\server" -Force
New-Item -ItemType Directory -Path "C:\DotNetCloud\cli" -Force
New-Item -ItemType Directory -Path "C:\DotNetCloud\data\files" -Force
New-Item -ItemType Directory -Path "C:\DotNetCloud\logs" -Force

# Download latest release
$Version = "1.0.0"
Invoke-WebRequest "https://github.com/LLabmik/DotNetCloud/releases/download/v${Version}/dotnetcloud-${Version}-win-x64.zip" `
  -OutFile "$env:TEMP\dotnetcloud.zip"

# Extract
Expand-Archive "$env:TEMP\dotnetcloud.zip" -DestinationPath "C:\DotNetCloud" -Force
```

#### Step 3: Build from Source (Alternative)

```powershell
# Clone the repository
git clone https://github.com/LLabmik/DotNetCloud.git
Set-Location DotNetCloud

# Publish the Core Server
dotnet publish src\Core\DotNetCloud.Core.Server\DotNetCloud.Core.Server.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output C:\DotNetCloud\server

# Publish the CLI
dotnet publish src\CLI\DotNetCloud.CLI\DotNetCloud.CLI.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output C:\DotNetCloud\cli

# Add CLI to PATH
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\DotNetCloud\cli", "Machine")
```

#### Step 4: Create Configuration

Create `C:\DotNetCloud\server\appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=your-secure-password-here"
  },
  "Kestrel": {
    "HttpPort": 5080,
    "HttpsPort": 5443,
    "EnableHttps": false
  },
  "Serilog": {
    "FilePath": "C:\\DotNetCloud\\logs\\dotnetcloud-.log"
  },
  "Files": {
    "StorageRoot": "C:\\DotNetCloud\\data\\files"
  }
}
```

For SQL Server with Windows Authentication:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=dotnetcloud;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

#### Step 5: Register as Windows Service

```powershell
# Create the Windows Service
New-Service -Name "DotNetCloud" `
  -BinaryPathName "C:\DotNetCloud\server\DotNetCloud.Core.Server.exe" `
  -DisplayName "DotNetCloud Core Server" `
  -Description "DotNetCloud self-hosted cloud platform" `
  -StartupType Automatic

# Set environment variables for the service
# Option 1: Use the appsettings.Production.json file (already done above)
# Option 2: Set via registry (advanced)

# Set the service to restart on failure
sc.exe failure DotNetCloud reset= 86400 actions= restart/5000/restart/10000/restart/30000

# Start the service
Start-Service DotNetCloud

# Check status
Get-Service DotNetCloud
```

**Alternative: Run as a console application for testing**

```powershell
Set-Location "C:\DotNetCloud\server"
$env:ASPNETCORE_ENVIRONMENT = "Production"
.\DotNetCloud.Core.Server.exe
```

#### Step 6: Run Initial Setup

```powershell
Set-Location "C:\DotNetCloud\cli"
.\dotnetcloud.exe setup
```

The wizard prompts for database, admin account, MFA, organization, and modules.

#### Step 7: Configure Reverse Proxy (Optional)

For production, use IIS as a reverse proxy. See [Reverse Proxy Configuration](#reverse-proxy-configuration) below.

For development or small deployments, Kestrel can serve directly on ports 80/443:

```json
{
  "Kestrel": {
    "HttpPort": 80,
    "HttpsPort": 443,
    "EnableHttps": true,
    "ListenAddresses": ["0.0.0.0"]
  }
}
```

---

### Directory Layout (Windows)

```
C:\DotNetCloud\
├── server\                     # Core Server binaries
│   ├── DotNetCloud.Core.Server.exe
│   ├── appsettings.json        # Default configuration
│   ├── appsettings.Production.json  # Your overrides
│   └── wwwroot\                # Static web assets
├── cli\                        # CLI tool
│   └── dotnetcloud.exe
├── modules\                    # Module host binaries
│   ├── files\
│   ├── chat\
│   └── example\
├── data\
│   └── files\                  # File storage root
└── logs\
    ├── dotnetcloud-20260303.log
    └── dotnetcloud-20260302.log
```

---

## Docker Installation

> **New to Docker?** See the [Docker Beginner Guide](DOCKER_BEGINNER_GUIDE.md) for a step-by-step walkthrough that explains everything from installing Docker to managing your deployment.

### Docker Compose (Recommended)

Create a `docker-compose.yml`:

```yaml
services:
  dotnetcloud:
    image: dotnetcloud/server:latest
    container_name: dotnetcloud
    restart: unless-stopped
    ports:
      - "8080:5080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=dotnetcloud;Username=dotnetcloud;Password=changeme
      - Files__StorageRoot=/data/files
      - Serilog__FilePath=/data/logs/dotnetcloud-.log
    volumes:
      - dotnetcloud-data:/data
    depends_on:
      db:
        condition: service_healthy

  db:
    image: postgres:16-alpine
    container_name: dotnetcloud-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: dotnetcloud
      POSTGRES_PASSWORD: changeme
      POSTGRES_DB: dotnetcloud
    volumes:
      - dotnetcloud-db:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U dotnetcloud"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  dotnetcloud-data:
  dotnetcloud-db:
```

Start:

```bash
docker compose up -d
```

Run initial setup:

```bash
docker exec -it dotnetcloud dotnetcloud setup
```

### Build from Source

```bash
git clone https://github.com/LLabmik/DotNetCloud.git
cd DotNetCloud
docker build -t dotnetcloud/server:local .
```

---

## Initial Setup Wizard

After installation, run the setup wizard to configure DotNetCloud for first use.

### Interactive Mode

```bash
# Linux
sudo -u dotnetcloud dotnetcloud setup

# Windows (PowerShell as Administrator)
dotnetcloud setup

# Docker
docker exec -it dotnetcloud dotnetcloud setup
```

### The Wizard Steps

1. **Database Configuration**
   - Select provider: PostgreSQL, SQL Server, or MariaDB
   - Enter connection string
   - Test connection
   - Run database migrations (creates all tables)

2. **Admin Account**
   - Username and email
   - Password (must meet complexity requirements)
   - TOTP MFA setup (scan QR code with authenticator app)

3. **Organization**
   - Organization name (e.g., "My Company")
   - Optional description

4. **TLS Configuration**
  - Enable/disable HTTPS on Kestrel
  - Public internet mode: Let's Encrypt automatic certificate
  - Private testing mode: generate a self-signed certificate automatically
  - Existing certificate mode: specify a custom certificate path

5. **Module Selection**
   - Files module (recommended)
   - Chat module
   - Example module (for developers)

6. **Collabora CODE** (Optional)
   - Install built-in Collabora CODE for document editing
   - Or configure an external Collabora server URL

7. **Save Configuration**
   - Writes configuration to `appsettings.Production.json` (or environment variables)
   - Seeds the database with default roles, permissions, and settings

### Non-Interactive Mode

For automated deployments:

```bash
dotnetcloud setup \
  --db-provider postgresql \
  --connection-string "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=secret" \
  --admin-username admin \
  --admin-email admin@example.com \
  --admin-password "YourStr0ng!Password" \
  --org-name "My Organization" \
  --modules files,chat \
  --no-interactive
```

---

## Reverse Proxy Configuration

In production, DotNetCloud should run behind a reverse proxy for TLS termination, security, and performance.

### nginx (Linux — Recommended)

Install nginx:

```bash
sudo apt install nginx
```

Create `/etc/nginx/sites-available/dotnetcloud`:

```nginx
upstream dotnetcloud {
    server 127.0.0.1:5080;
}

server {
    listen 80;
    listen [::]:80;
    server_name cloud.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name cloud.example.com;

    ssl_certificate /etc/letsencrypt/live/cloud.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/cloud.example.com/privkey.pem;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "SAMEORIGIN" always;

    # Disable request body size limit (chunked uploads handle large files)
    client_max_body_size 0;

    # Proxy timeouts for long operations
    proxy_connect_timeout 300;
    proxy_send_timeout 300;
    proxy_read_timeout 300;

    # Main application
    location / {
        proxy_pass http://dotnetcloud;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_buffering off;
    }

    # SignalR WebSocket support
    location /hubs/ {
        proxy_pass http://dotnetcloud;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400;
    }
}
```

Enable and reload:

```bash
sudo ln -sf /etc/nginx/sites-available/dotnetcloud /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

### Apache (Linux)

```bash
sudo apt install apache2
sudo a2enmod proxy proxy_http proxy_wstunnel ssl rewrite headers
```

Create `/etc/apache2/sites-available/dotnetcloud.conf`:

```apache
<VirtualHost *:80>
    ServerName cloud.example.com
    RewriteEngine On
    RewriteRule ^(.*)$ https://%{HTTP_HOST}$1 [R=301,L]
</VirtualHost>

<VirtualHost *:443>
    ServerName cloud.example.com

    SSLEngine On
    SSLCertificateFile /etc/letsencrypt/live/cloud.example.com/fullchain.pem
    SSLCertificateKeyFile /etc/letsencrypt/live/cloud.example.com/privkey.pem

    ProxyPreserveHost On
    RequestHeader set X-Forwarded-Proto "https"

    # Main application
    ProxyPass / http://127.0.0.1:5080/
    ProxyPassReverse / http://127.0.0.1:5080/

    # SignalR WebSocket
    RewriteEngine On
    RewriteCond %{HTTP:Upgrade} =websocket [NC]
    RewriteRule /hubs/(.*) ws://127.0.0.1:5080/hubs/$1 [P,L]

    # Security headers
    Header always set Strict-Transport-Security "max-age=31536000; includeSubDomains"
    Header always set X-Content-Type-Options "nosniff"
    Header always set X-Frame-Options "SAMEORIGIN"
</VirtualHost>
```

Enable and restart:

```bash
sudo a2ensite dotnetcloud
sudo systemctl restart apache2
```

### IIS (Windows)

1. Install the [.NET Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) (includes ASP.NET Core Module for IIS)

2. Create a new IIS website:
   - **Site name:** DotNetCloud
   - **Physical path:** `C:\DotNetCloud\server`
   - **Binding:** HTTPS, port 443, hostname `cloud.example.com`
   - **SSL certificate:** Select your certificate

3. DotNetCloud includes a `web.config` for IIS ANCM (ASP.NET Core Module):

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <location path="." inheritInChildApplications="false">
       <system.webServer>
         <handlers>
           <add name="aspNetCore" path="*" verb="*"
                modules="AspNetCoreModuleV2" resourceType="Unspecified" />
         </handlers>
         <aspNetCore processPath=".\DotNetCloud.Core.Server.exe"
                     stdoutLogEnabled="true"
                     stdoutLogFile=".\logs\stdout"
                     hostingModel="InProcess">
           <environmentVariables>
             <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
           </environmentVariables>
         </aspNetCore>
         <webSocket enabled="true" />
       </system.webServer>
     </location>
   </configuration>
   ```

4. Ensure the IIS application pool identity has read/write access to:
   - `C:\DotNetCloud\data\files`
   - `C:\DotNetCloud\logs`

5. Enable WebSocket Protocol in IIS:
   - Server Manager → Add Roles and Features → Web Server → Application Development → WebSocket Protocol

---

## TLS / Let's Encrypt

### Setup Wizard TLS Modes

The setup wizard now supports three HTTPS certificate modes:

1. **Public internet (Let's Encrypt)**
  - Use when your server is publicly reachable and has a real DNS name.
2. **Private testing (self-signed)**
  - Use for LAN/private environments where you do not want to expose the server publicly.
  - The wizard generates a self-signed PFX certificate and configures DotNetCloud to use it.
  - Default system install path: `/etc/dotnetcloud/certs/dotnetcloud-selfsigned.pfx`
3. **Existing certificate file**
  - Use your own PFX/PEM certificate path.

> Note: Browsers and clients will show trust warnings for self-signed certificates until you trust the certificate on each device.

### Linux with Certbot

```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx

# Obtain certificate (nginx plugin)
sudo certbot --nginx -d cloud.example.com

# Auto-renewal is configured automatically
sudo certbot renew --dry-run
```

### Linux with Certbot (Apache)

```bash
sudo apt install certbot python3-certbot-apache
sudo certbot --apache -d cloud.example.com
```

### Windows with win-acme

1. Download [win-acme](https://www.win-acme.com/)
2. Run `wacs.exe` as Administrator
3. Select your IIS site
4. Certificate is installed automatically and renewed on schedule

### Kestrel Direct TLS (No Reverse Proxy)

For small deployments without a reverse proxy, configure Kestrel directly:

```json
{
  "Kestrel": {
    "EnableHttps": true,
    "HttpsPort": 443,
    "CertificatePath": "/etc/letsencrypt/live/cloud.example.com/fullchain.pem",
    "CertificateKeyPath": "/etc/letsencrypt/live/cloud.example.com/privkey.pem"
  }
}
```

### Private Testing TLS (Self-Signed, No Public Exposure)

If you choose **Private testing (self-signed)** in the setup wizard:

1. Enter a private hostname or LAN IP (for example `mint22` or `192.168.0.14`).
2. The wizard generates a self-signed PFX certificate.
3. DotNetCloud binds HTTPS using that generated certificate.

Recommended verification:

```bash
sudo ls -l /etc/dotnetcloud/certs/dotnetcloud-selfsigned.pfx
curl -kfsS https://localhost:5443/health/live
```

Use `-k` only for local testing when the certificate is not trusted yet.

### Trusting the Self-Signed Certificate (Private Testing)

For day-to-day private testing, import the generated certificate into each client trust store instead of using `-k`.

#### Linux Clients (system trust store)

On the DotNetCloud server, export the certificate from PFX to CRT:

```bash
sudo openssl pkcs12 \
  -in /etc/dotnetcloud/certs/dotnetcloud-selfsigned.pfx \
  -clcerts -nokeys \
  -out /tmp/dotnetcloud-selfsigned.crt \
  -passin pass:
```

Copy `dotnetcloud-selfsigned.crt` to each Linux client, then install:

```bash
sudo cp dotnetcloud-selfsigned.crt /usr/local/share/ca-certificates/
sudo update-ca-certificates
```

#### Windows Clients (Trusted Root)

1. Export `dotnetcloud-selfsigned.crt` as shown above.
2. Open `certlm.msc` as Administrator.
3. Go to `Trusted Root Certification Authorities` → `Certificates`.
4. Import `dotnetcloud-selfsigned.crt`.
5. Restart browsers/clients.

#### Android Devices / Emulators

1. Export `dotnetcloud-selfsigned.crt` as shown above.
2. Transfer the `.crt` file to the Android device/emulator.
3. Open `Settings` → `Security` (or `Security & privacy`) → `Encryption & credentials` → `Install a certificate` → `CA certificate`.
4. Select `dotnetcloud-selfsigned.crt` and confirm installation.

Notes:

- Some Android apps do not trust user-installed CAs unless explicitly configured.
- For browser testing and debugging, this is usually sufficient.

---

## Verifying the Installation

### Fast Redeploy (Existing Bare-Metal Host)

Use this when `dotnetcloud.service` is already installed and you want to deploy the latest source checkout quickly.

This is the preferred workflow for local server development. You do not need to push to GitHub or re-run the remote `install.sh` just to apply local code changes on the same machine.

Fastest path (one command):

```bash
./tools/redeploy-baremetal.sh
```

This helper performs publish + service restart + health verification and fails fast if any step fails.
By default it probes both local HTTPS (`https://localhost:15443/health/live`) and installer-default local HTTP (`http://localhost:5080/health/live`). You can override with `HEALTH_URL=...`.

Maintenance rule for contributors:

- If you change the bare-metal deployment process used by local redeploys, update `tools/install.sh` to keep first-install and upgrade behavior in sync for other machines.
- Validate both paths when process changes are made:
  - local source redeploy (`./tools/redeploy-baremetal.sh`)
  - GitHub-based installer (`tools/install.sh`, including fresh install/upgrade expectations)

1. Confirm the service unit points to your expected publish directory:

```bash
systemctl cat dotnetcloud.service --no-pager
```

2. Publish the latest server build to the configured working directory output (project-local bare-metal path):

```bash
dotnet publish src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj \
  --configuration Release \
  --output artifacts/publish/server-baremetal
```

3. Restart and verify service state:

```bash
systemctl restart dotnetcloud.service
systemctl status dotnetcloud.service --no-pager
```

4. Verify liveness endpoint (HTTPS):

```bash
curl -kfsS https://localhost:15443/health/live
```

Expected result:

```json
{
  "status": "Healthy"
}
```

If your service uses different ports or paths, trust `systemctl cat dotnetcloud.service` and follow the `ExecStart`, `WorkingDirectory`, and `EnvironmentFile` values from that unit.

### Health Check

```bash
curl -s http://localhost:5080/health | jq .
```

Expected response:

```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "startup": "Healthy"
  }
}
```

### Web UI

Open `https://cloud.example.com` in your browser. You should see the DotNetCloud login page.

### CLI Status

```bash
dotnetcloud status
```

Expected output:

```
DotNetCloud Status
==================
Server:    Running
Database:  Connected (PostgreSQL 16.2)
Modules:
  files    Running  v1.0.0
  chat     Running  v1.0.0
  example  Running  v1.0.0
```

### Service Logs

```bash
# Linux (systemd)
sudo journalctl -u dotnetcloud -f

# Linux (log file)
tail -f /var/log/dotnetcloud/dotnetcloud-$(date +%Y%m%d).log

# Windows (Event Viewer or log file)
Get-Content "C:\DotNetCloud\logs\dotnetcloud-$(Get-Date -Format 'yyyyMMdd').log" -Wait
```

---

## Next Steps

After a successful installation:

1. **[Configure the server](CONFIGURATION.md)** — tune Kestrel, logging, rate limiting, CORS, and more
2. **[Set up Collabora](../COLLABORA.md)** — enable browser-based document editing
3. **[Configure file storage](../CONFIGURATION.md)** — quotas, trash retention, version limits
4. **[Set up backups](../BACKUP.md)** — schedule automatic database and file backups
5. **[Upgrade procedures](UPGRADING.md)** — how to update DotNetCloud to newer versions
6. **[Install the sync client](../../user/SYNC_CLIENT.md)** — sync files to desktops

---

## Troubleshooting

### DotNetCloud Won't Start

1. **Check logs:** `journalctl -u dotnetcloud -n 50` (Linux) or Event Viewer (Windows)
2. **Database connection:** Verify the connection string and that the database is running
3. **Port conflict:** Ensure port 5080 is not in use: `ss -tlnp | grep 5080` (Linux) or `netstat -ano | findstr 5080` (Windows)
4. **Permissions:** Verify the service user has read/write access to data and log directories
5. **.NET runtime:** Verify `dotnet --info` shows ASP.NET Core 10.0

### Cannot Connect via Browser

1. **Reverse proxy:** Check nginx/Apache/IIS error logs
2. **Firewall:** Verify ports 80/443 are open: `sudo ufw status` (Linux) or `Get-NetFirewallRule` (Windows)
3. **DNS:** Verify the domain resolves to your server: `nslookup cloud.example.com`
4. **TLS:** Check certificate validity: `openssl s_client -connect cloud.example.com:443`

### Database Migration Errors

```bash
# Re-run setup to apply migrations
dotnetcloud setup --db-provider postgresql \
  --connection-string "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=secret"
```

### Module Won't Load

```bash
# Check module status
dotnetcloud module list

# View module-specific logs
dotnetcloud logs files

# Restart a module
dotnetcloud module restart files
```

### SignalR / WebSocket Errors

1. Verify WebSocket support is enabled in the reverse proxy
2. Check that the `/hubs/` location block has `Upgrade` and `Connection` headers
3. Verify `proxy_read_timeout` is set to a high value (86400 for persistent connections)

---

## Related Documentation

- [Server Configuration Reference](CONFIGURATION.md)
- [Upgrading DotNetCloud](UPGRADING.md)
- [Docker Beginner Guide](DOCKER_BEGINNER_GUIDE.md)
- [Files Module Configuration](../CONFIGURATION.md)
- [Collabora Administration](../COLLABORA.md)
- [Backup & Restore](../BACKUP.md)
- [Architecture Overview](../../architecture/ARCHITECTURE.md)

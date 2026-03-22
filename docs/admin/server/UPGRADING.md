# DotNetCloud Server — Upgrade Guide

> **Last Updated:** 2026-03-03  
> **Applies To:** DotNetCloud 1.0.x  
> **Audience:** System administrators

---

## Table of Contents

1. [Before You Upgrade](#before-you-upgrade)
2. [Upgrade on Linux](#upgrade-on-linux)
3. [Upgrade on Windows](#upgrade-on-windows)
4. [Upgrade Docker](#upgrade-docker)
5. [Database Migrations](#database-migrations)
6. [Rolling Back](#rolling-back)
7. [Version Compatibility](#version-compatibility)

---

## Before You Upgrade

### Pre-Upgrade Checklist

1. **Read the release notes** — check for breaking changes, required migration steps, and new configuration options
2. **Back up the database**

   ```bash
   # PostgreSQL
   pg_dump -U dotnetcloud -d dotnetcloud -F c -f /backup/dotnetcloud-pre-upgrade.dump

   # SQL Server
   sqlcmd -S localhost -U sa -P 'password' -Q "BACKUP DATABASE dotnetcloud TO DISK='/backup/dotnetcloud-pre-upgrade.bak'"
   ```

3. **Back up file storage**

   ```bash
   # Linux
   sudo tar -czf /backup/files-pre-upgrade.tar.gz /var/lib/dotnetcloud/files
   ```

   ```powershell
   # Windows
   Compress-Archive -Path "C:\DotNetCloud\data\files" -DestinationPath "D:\Backup\files-pre-upgrade.zip"
   ```

4. **Back up configuration**

   ```bash
   sudo cp /etc/dotnetcloud/appsettings.Production.json /backup/appsettings.Production.json.bak
   ```

5. **Note the current version**

   ```bash
   dotnetcloud --version
   ```

6. **Schedule downtime** — upgrades typically require 1–5 minutes of downtime

---

## Upgrade on Linux

### Package Manager Upgrade (If Installed via Package)

```bash
# Ubuntu/Debian
sudo apt update
sudo apt upgrade dotnetcloud

# RHEL/CentOS/Fedora
sudo dnf upgrade dotnetcloud
```

The package manager handles:
- Stopping the service
- Replacing binaries
- Running database migrations
- Restarting the service

### Manual Upgrade

#### Step 1: Download the New Release

```bash
VERSION="1.1.0"
wget "https://github.com/LLabmik/DotNetCloud/releases/download/v${VERSION}/dotnetcloud-${VERSION}-linux-x64.tar.gz" \
  -O /tmp/dotnetcloud-${VERSION}.tar.gz
```

#### Step 2: Stop the Service

```bash
sudo systemctl stop dotnetcloud
```

#### Step 3: Replace Binaries

```bash
# Back up current installation (just in case)
sudo cp -r /opt/dotnetcloud/server /opt/dotnetcloud/server.bak

# Extract new version
sudo tar -xzf /tmp/dotnetcloud-${VERSION}.tar.gz -C /opt/dotnetcloud --strip-components=1

# Restore ownership
sudo chown -R dotnetcloud:dotnetcloud /opt/dotnetcloud
```

#### Step 4: Run Database Migrations

Migrations run automatically on startup, but you can run them explicitly:

```bash
sudo -u dotnetcloud dotnetcloud setup \
  --db-provider postgresql \
  --connection-string "Host=localhost;Database=dotnetcloud;Username=dotnetcloud;Password=your-password" \
  --migrate-only
```

#### Step 5: Start the Service

```bash
sudo systemctl start dotnetcloud

# Verify
sudo systemctl status dotnetcloud
dotnetcloud status
```

#### Step 6: Verify

```bash
# Check version
dotnetcloud --version

# Check health
curl -s http://localhost:5080/health | jq .

# Check logs for errors
sudo journalctl -u dotnetcloud -n 50 --no-pager
```

#### Step 7: Clean Up

After verifying the upgrade works:

```bash
sudo rm -rf /opt/dotnetcloud/server.bak
```

---

## Upgrade on Windows

### MSI Installer Upgrade

1. Download the new MSI from the releases page
2. Run the installer — it detects the existing installation and upgrades in place
3. The installer stops the service, replaces binaries, runs migrations, and restarts

### Manual Upgrade

#### Step 1: Stop the Service

```powershell
Stop-Service DotNetCloud
```

#### Step 2: Back Up Current Binaries

```powershell
Copy-Item -Path "C:\DotNetCloud\server" -Destination "C:\DotNetCloud\server.bak" -Recurse
```

#### Step 3: Extract New Version

```powershell
$Version = "1.1.0"
Invoke-WebRequest "https://github.com/LLabmik/DotNetCloud/releases/download/v${Version}/dotnetcloud-${Version}-win-x64.zip" `
  -OutFile "$env:TEMP\dotnetcloud-${Version}.zip"

# Extract (overwrite existing files)
Expand-Archive "$env:TEMP\dotnetcloud-${Version}.zip" -DestinationPath "C:\DotNetCloud" -Force
```

**Important:** Your `appsettings.Production.json` is not overwritten because it's not in the release archive. The default `appsettings.json` may be updated with new settings.

#### Step 4: Run Database Migrations

```powershell
Set-Location "C:\DotNetCloud\cli"
.\dotnetcloud.exe setup --migrate-only
```

#### Step 5: Start the Service

```powershell
Start-Service DotNetCloud

# Verify
Get-Service DotNetCloud
.\dotnetcloud.exe status
```

#### Step 6: Verify

```powershell
.\dotnetcloud.exe --version
Invoke-RestMethod -Uri "http://localhost:5080/health"
```

#### Step 7: Clean Up

```powershell
Remove-Item -Path "C:\DotNetCloud\server.bak" -Recurse -Force
```

---

## Upgrade Docker

### Docker Compose

```bash
# Pull the new image
docker compose pull

# Recreate the container (database migrations run automatically on startup)
docker compose up -d

# Verify
docker compose logs -f dotnetcloud --tail=50
docker exec dotnetcloud dotnetcloud --version
```

### Single Container

```bash
docker pull dotnetcloud/server:latest
docker stop dotnetcloud
docker rm dotnetcloud
docker run -d --name dotnetcloud \
  -p 8080:5080 \
  -v dotnetcloud-data:/data \
  -e ConnectionStrings__DefaultConnection="Host=db;Database=dotnetcloud;Username=dotnetcloud;Password=changeme" \
  dotnetcloud/server:latest
```

---

## Database Migrations

### Automatic Migrations

By default, DotNetCloud applies pending database migrations automatically on startup. This is controlled by the `DbInitializer` classes for each module.

### Manual Migration

If you prefer to apply migrations manually (recommended for large production databases):

1. Stop the service
2. Run migrations:

   ```bash
   dotnetcloud setup --migrate-only
   ```

3. Start the service

### Checking Migration Status

```bash
# List pending migrations (requires EF Core tools)
dotnet ef migrations list \
  --project src/Core/DotNetCloud.Core.Data \
  --startup-project src/Core/DotNetCloud.Core.Server
```

### Migration Failures

If a migration fails:

1. Check the error in the logs
2. Restore the database from the pre-upgrade backup
3. Report the issue
4. Roll back to the previous version (see below)

---

## Rolling Back

### Step 1: Stop the Service

```bash
# Linux
sudo systemctl stop dotnetcloud

# Windows
Stop-Service DotNetCloud
```

### Step 2: Restore Database

```bash
# PostgreSQL
pg_restore -U dotnetcloud -d dotnetcloud --clean /backup/dotnetcloud-pre-upgrade.dump

# SQL Server
sqlcmd -S localhost -U sa -P 'password' -Q "RESTORE DATABASE dotnetcloud FROM DISK='/backup/dotnetcloud-pre-upgrade.bak' WITH REPLACE"
```

### Step 3: Restore Binaries

```bash
# Linux
sudo rm -rf /opt/dotnetcloud/server
sudo mv /opt/dotnetcloud/server.bak /opt/dotnetcloud/server

# Windows
Remove-Item -Path "C:\DotNetCloud\server" -Recurse -Force
Rename-Item -Path "C:\DotNetCloud\server.bak" -NewName "server"
```

### Step 4: Restore File Storage (If Needed)

Only necessary if the upgrade modified file storage:

```bash
# Linux
sudo tar -xzf /backup/files-pre-upgrade.tar.gz -C /
```

### Step 5: Start the Service

```bash
# Linux
sudo systemctl start dotnetcloud

# Windows
Start-Service DotNetCloud
```

---

## Version Compatibility

### Semantic Versioning

DotNetCloud follows semantic versioning (`MAJOR.MINOR.PATCH`):

| Version Change | Meaning | Database Migrations | Config Changes |
|---|---|---|---|
| **Patch** (1.0.0 → 1.0.1) | Bug fixes only | None | None |
| **Minor** (1.0.x → 1.1.0) | New features, backward-compatible | Possible (additive) | New optional settings |
| **Major** (1.x → 2.0.0) | Breaking changes | Likely (may alter schema) | Settings may be renamed/removed |

### Upgrade Path

- **Patch upgrades:** Always safe, no special steps
- **Minor upgrades:** Read release notes, back up database
- **Major upgrades:** Follow the dedicated migration guide in the release notes

### Skipping Versions

- **Patch versions:** Can be skipped safely (1.0.0 → 1.0.5)
- **Minor versions:** Can be skipped safely (1.0.x → 1.3.0) — all intermediate migrations are applied
- **Major versions:** Must upgrade sequentially (1.x → 2.x → 3.x) — do not skip major versions

### .NET Runtime Compatibility

| DotNetCloud Version | Required .NET Runtime |
|---|---|
| 1.0.x | .NET 10 |

When a new DotNetCloud major version requires a newer .NET runtime, the release notes will include .NET upgrade instructions.

---

## Related Documentation

- [Installation Guide](INSTALLATION.md)
- [Configuration Reference](CONFIGURATION.md)
- [Backup & Restore](../BACKUP.md)

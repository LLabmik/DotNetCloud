# DotNetCloud — Docker Deployment Guide (Beginner-Friendly)

> **Last Updated:** 2026-03-22
> **Audience:** Self-hosters who are new to Docker
> **Time to complete:** ~15 minutes

---

## Table of Contents

1. [What Is Docker?](#what-is-docker)
2. [Install Docker](#install-docker)
3. [Deploy DotNetCloud](#deploy-dotnetcloud)
4. [Run the Setup Wizard](#run-the-setup-wizard)
5. [Open DotNetCloud](#open-dotnetcloud)
6. [Everyday Commands](#everyday-commands)
7. [Understanding Your Data](#understanding-your-data)
8. [Updating DotNetCloud](#updating-dotnetcloud)
9. [Adding HTTPS](#adding-https)
10. [Troubleshooting](#troubleshooting)
11. [Next Steps](#next-steps)

---

## What Is Docker?

Docker runs applications inside isolated "containers." Think of a container as a lightweight, self-contained package that includes DotNetCloud, its database, and everything they need to run — without installing any of that directly on your machine.

**Why use Docker for DotNetCloud?**

- **No manual setup.** You don't install .NET, PostgreSQL, or configure services yourself. Docker handles all of it.
- **Easy cleanup.** If you want to remove DotNetCloud, just delete the containers. Your system stays clean.
- **Easy updates.** Pull a new image, restart the container, done.
- **Same on every machine.** Works identically on Linux, Windows, or macOS.

**Key terms** you'll see in this guide:

| Term | What It Means |
|---|---|
| **Image** | A blueprint for running software (like an installer file) |
| **Container** | A running instance of an image (like a running program) |
| **Volume** | A folder where Docker saves data that survives container restarts |
| **Docker Compose** | A tool that starts multiple containers together from a single config file |

---

## Install Docker

### Linux (Ubuntu, Debian, Mint)

```bash
# Install Docker
sudo apt update
sudo apt install -y docker.io docker-compose-plugin

# Let your user run Docker without sudo
sudo usermod -aG docker $USER
```

**Important:** Log out and log back in (or reboot) for the group change to take effect.

Verify it works:

```bash
docker --version
docker compose version
```

You should see version numbers for both. If `docker compose version` fails, your Docker is too old — see the [Docker official install guide](https://docs.docker.com/engine/install/) for updated packages.

### Windows

1. Download **Docker Desktop** from [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop)
2. Run the installer and follow the prompts
3. When asked, choose **WSL 2 backend** (recommended)
4. Restart your computer when prompted
5. Open a terminal (PowerShell or Command Prompt) and verify:

```powershell
docker --version
docker compose version
```

> **Note:** Docker Desktop on Windows requires Windows 10/11 Pro, Enterprise, or Education with WSL 2. Windows Home works too with WSL 2 enabled. You need at least 4 GB of RAM (8 GB recommended).

### macOS

1. Download **Docker Desktop** from [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop)
2. Choose **Mac with Intel chip** or **Mac with Apple chip** as appropriate
3. Open the `.dmg` file and drag Docker to Applications
4. Launch Docker Desktop from Applications
5. Verify in a terminal:

```bash
docker --version
docker compose version
```

---

## Deploy DotNetCloud

### Step 1: Create a Project Folder

Pick a location to store your DotNetCloud configuration. You only need one file.

```bash
mkdir ~/dotnetcloud && cd ~/dotnetcloud
```

On Windows (PowerShell):

```powershell
mkdir $HOME\dotnetcloud; cd $HOME\dotnetcloud
```

### Step 2: Create the Compose File

Create a file called `docker-compose.yml` in that folder with this content:

```yaml
services:
  # --- DotNetCloud Server ---
  dotnetcloud:
    image: dotnetcloud/server:latest
    container_name: dotnetcloud
    restart: unless-stopped
    ports:
      - "8080:5080"       # Access DotNetCloud at http://localhost:8080
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=dotnetcloud;Username=dotnetcloud;Password=changeme
      - Files__StorageRoot=/data/files
      - Serilog__FilePath=/data/logs/dotnetcloud-.log
    volumes:
      - dotnetcloud-data:/data       # Your files and logs
    depends_on:
      db:
        condition: service_healthy   # Wait for database to be ready

  # --- PostgreSQL Database ---
  db:
    image: postgres:16-alpine
    container_name: dotnetcloud-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: dotnetcloud
      POSTGRES_PASSWORD: changeme        # ⚠️ Change this password!
      POSTGRES_DB: dotnetcloud
    volumes:
      - dotnetcloud-db:/var/lib/postgresql/data   # Database files
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U dotnetcloud"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  dotnetcloud-data:    # Stores uploaded files and logs
  dotnetcloud-db:      # Stores the PostgreSQL database
```

> **⚠️ Change the password!** Replace `changeme` in **both** places (the `ConnectionStrings__DefaultConnection` line and the `POSTGRES_PASSWORD` line) with a strong password. Both values must match.

### Step 3: Start DotNetCloud

```bash
docker compose up -d
```

What this does:
- Downloads the DotNetCloud and PostgreSQL images (first time only, may take a few minutes)
- Creates and starts both containers
- The `-d` flag runs them in the background

You can watch the startup progress:

```bash
docker compose logs -f
```

Press `Ctrl+C` to stop watching logs (the containers keep running).

Wait until you see a line like `Now listening on: http://[::]:5080` in the DotNetCloud logs — that means it's ready.

---

## Run the Setup Wizard

Before you can use DotNetCloud, you need to create your admin account and configure basic settings:

```bash
docker exec -it dotnetcloud dotnetcloud setup
```

The wizard will ask you for:

1. **Admin username and password** — this is your first user account
2. **Organization name** — a display name for your instance
3. **Which modules to enable** — at minimum, enable "files"

Once setup finishes, DotNetCloud is ready to use.

> **Tip:** For automated/scripted deployments, you can skip the interactive wizard:
> ```bash
> docker exec -it dotnetcloud dotnetcloud setup \
>   --admin-username admin \
>   --admin-email admin@example.com \
>   --admin-password "YourStr0ng!Password" \
>   --org-name "My Cloud" \
>   --modules files,chat \
>   --no-interactive
> ```

---

## Open DotNetCloud

Open your browser and go to:

```
http://localhost:8080
```

Log in with the admin username and password you set during setup.

If you're accessing DotNetCloud from another device on your network, use your server's IP or hostname instead of `localhost`:

```
http://192.168.1.100:8080
```

---

## Everyday Commands

Here are the commands you'll actually use day-to-day. Run all of these from the folder where your `docker-compose.yml` lives.

### Check Status

```bash
# Are the containers running?
docker compose ps

# Check DotNetCloud health
docker exec dotnetcloud dotnetcloud status
```

### View Logs

```bash
# See recent logs from all containers
docker compose logs --tail=50

# Follow DotNetCloud logs in real-time
docker compose logs -f dotnetcloud

# Follow database logs
docker compose logs -f db
```

### Restart

```bash
# Restart everything
docker compose restart

# Restart only DotNetCloud (not the database)
docker compose restart dotnetcloud
```

### Stop

```bash
# Stop all containers (keeps data)
docker compose stop

# Start them again
docker compose start
```

### Full Shutdown (Remove Containers)

```bash
# Remove containers but KEEP your data
docker compose down

# Start fresh
docker compose up -d
```

> **⚠️ Never use `docker compose down -v`** unless you want to **permanently delete all your data** (files, database, everything). The `-v` flag removes volumes.

---

## Understanding Your Data

All your data lives in Docker **volumes** — persistent storage that survives container restarts, updates, and even removal.

| Volume | Contains | What Happens If Deleted |
|---|---|---|
| `dotnetcloud-data` | Your uploaded files, logs | **All files lost permanently** |
| `dotnetcloud-db` | The PostgreSQL database (users, settings, metadata) | **All users and settings lost** |

### Where Are Volumes Stored on Disk?

Docker manages volume storage automatically. To see where:

```bash
docker volume inspect dotnetcloud-data
```

The `Mountpoint` field shows the physical path on your host machine.

### Backing Up Your Data

**Database backup:**

```bash
docker exec dotnetcloud-db pg_dump -U dotnetcloud dotnetcloud > backup.sql
```

**Restore from backup:**

```bash
docker exec -i dotnetcloud-db psql -U dotnetcloud dotnetcloud < backup.sql
```

**File storage backup:**

```bash
# Copy files out of the volume to a local folder
docker cp dotnetcloud:/data/files ./files-backup
```

For comprehensive backup strategies, see the [Backup & Restore guide](../BACKUP.md).

---

## Updating DotNetCloud

When a new version of DotNetCloud is released:

```bash
# Pull the latest image
docker compose pull

# Recreate the container with the new image (database is untouched)
docker compose up -d

# Verify the new version
docker exec dotnetcloud dotnetcloud --version
```

That's it. Database migrations run automatically on startup. Your data, files, and settings are preserved.

> **Tip:** Before updating, it's good practice to back up your database first (see the section above).

### Pinning a Specific Version

If you prefer to control exactly which version you're running, change the image tag in `docker-compose.yml`:

```yaml
    image: dotnetcloud/server:1.2.0    # Pin to specific version
```

Then run `docker compose up -d` to switch to that version.

---

## Adding HTTPS

Docker runs DotNetCloud on plain HTTP by default. For HTTPS, you have two options:

### Option A: Reverse Proxy (Recommended)

Put a reverse proxy (nginx, Caddy, Apache) in front of DotNetCloud. The proxy handles HTTPS certificates; DotNetCloud stays on HTTP internally.

This is the standard approach for production deployments. See:

- [Reverse Proxy Beginner Guide](REVERSE_PROXY_BEGINNER_GUIDE.md) — step-by-step Apache or Caddy setup
- [Full Installation Guide — Reverse Proxy section](INSTALLATION.md#reverse-proxy-configuration) — nginx, Apache, and IIS examples

The proxy connects to `http://localhost:8080` (or whatever port you mapped).

### Option B: Caddy as a Docker Sidecar

The simplest all-Docker solution is adding Caddy to your compose file. Caddy automatically provisions HTTPS certificates via Let's Encrypt.

Add this to your `docker-compose.yml`:

```yaml
  caddy:
    image: caddy:2-alpine
    container_name: dotnetcloud-caddy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - dotnetcloud
```

And add the volumes at the bottom:

```yaml
volumes:
  dotnetcloud-data:
  dotnetcloud-db:
  caddy-data:
  caddy-config:
```

Create a `Caddyfile` in the same folder:

```
cloud.example.com {
    reverse_proxy dotnetcloud:5080
}
```

Replace `cloud.example.com` with your actual domain. Caddy will automatically get a Let's Encrypt certificate for that domain.

> **Requirements:** Your domain must point to your server's public IP, and ports 80 and 443 must be open on your firewall and forwarded to this machine.

When using the Caddy sidecar, remove the `ports: - "8080:5080"` line from the `dotnetcloud` service — you'll access DotNetCloud through Caddy instead.

---

## Troubleshooting

### Container Won't Start

```bash
# Check what's happening
docker compose logs dotnetcloud
```

Common causes:
- **Database not ready yet:** DotNetCloud waits for PostgreSQL's health check, but if the database fails to start, DotNetCloud can't start either. Check `docker compose logs db`.
- **Port already in use:** If port 8080 is taken, change the port mapping in `docker-compose.yml` (e.g., `"9090:5080"`).
- **Password mismatch:** The password in `ConnectionStrings__DefaultConnection` must match `POSTGRES_PASSWORD`.

### "Connection Refused" in Browser

1. Verify containers are running: `docker compose ps`
2. Check that DotNetCloud shows as "healthy" (not "starting" or "unhealthy")
3. Try `http://localhost:8080/health/live` — if this returns `Healthy`, the server is working
4. If accessing remotely, check your firewall allows port 8080

### Database Connection Errors

```bash
# Can DotNetCloud reach the database?
docker exec dotnetcloud dotnetcloud status

# Is the database running?
docker compose ps db

# Check database logs
docker compose logs db
```

### Out of Disk Space

```bash
# See how much space Docker is using
docker system df

# Remove unused images and build cache (safe to run)
docker system prune
```

> **Warning:** `docker system prune` only removes things that aren't in use. It won't delete your running containers or their data volumes.

### Need to Start Over Completely

If something is badly broken and you want a clean slate:

```bash
# Stop and remove containers AND all data
docker compose down -v

# Start fresh
docker compose up -d

# Re-run setup
docker exec -it dotnetcloud dotnetcloud setup
```

> **⚠️ This permanently deletes all your files, users, and settings.** Only do this if you have a backup or truly want to start over.

---

## Next Steps

- [Server Configuration](CONFIGURATION.md) — customize logging, rate limiting, CORS, telemetry, and more
- [Collabora Administration](../COLLABORA.md) — enable browser-based document editing
- [Files Module Configuration](../CONFIGURATION.md) — quotas, trash retention, storage settings
- [Backup & Restore](../BACKUP.md) — schedule automatic backups
- [Upgrading Guide](UPGRADING.md) — version compatibility and rollback procedures

---

## Related Documentation

- [Full Installation Guide](INSTALLATION.md) — covers Linux bare-metal, Windows, and Docker
- [Reverse Proxy Beginner Guide](REVERSE_PROXY_BEGINNER_GUIDE.md) — Apache and Caddy setup
- [Architecture Overview](../../architecture/ARCHITECTURE.md) — how DotNetCloud works internally

#!/usr/bin/env bash
# deploy.sh — Deploy DotNetCloud server and module hosts to production
# Run as: sudo ./scripts/deploy.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
DEPLOY_DIR="/opt/dotnetcloud/server"
MODULES_DIR="$DEPLOY_DIR/modules"
SERVICE_USER="dotnetcloud"
CONFIG="${1:-Release}"

echo "=== Deploying DotNetCloud ($CONFIG) ==="

# Stop the service first so files aren't locked
echo "[1/7] Stopping service..."
systemctl stop dotnetcloud 2>/dev/null || true

# Build everything once — handles dependency ordering correctly, no file contention
echo "[2/7] Building solution..."
dotnet build "$REPO_ROOT/DotNetCloud.CI.slnf" -c "$CONFIG" -p:DebugType=None -p:DebugSymbols=false

# Publish Core.Server (no-build since already compiled)
echo "[3/7] Publishing Core.Server..."
dotnet publish "$REPO_ROOT/src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj" -c "$CONFIG" -o "$DEPLOY_DIR" --no-self-contained --no-build -p:DebugType=None -p:DebugSymbols=false

# Publish module data assemblies needed by Core.Server for migrations
echo "[4/7] Publishing module data assemblies (needed for migrations)..."
dotnet publish "$REPO_ROOT/src/Modules/AI/DotNetCloud.Modules.AI.Data.SqlServer/DotNetCloud.Modules.AI.Data.SqlServer.csproj" -c "$CONFIG" -o "$DEPLOY_DIR" --no-self-contained --no-build -p:DebugType=None -p:DebugSymbols=false 2>&1 | sed "s/^/[AI.Data.SqlServer] /" &

# Publish module hosts in parallel (no-build — safe to parallelize, no file contention)
echo "[5/7] Publishing module hosts (parallel, no-build)..."
for module in Contacts Calendar Chat Files Notes Tracks Music Photos Video Search Bookmarks Email About AI; do
    module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
    (
        dotnet publish "$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/DotNetCloud.Modules.$module.Host.csproj" -c "$CONFIG" -o "$MODULES_DIR/dotnetcloud.$module_lower" --no-self-contained --no-build -p:DebugType=None -p:DebugSymbols=false 2>&1 | sed "s/^/[$module] /"
    ) &
done
wait
echo "[5/7] All module hosts published."

# Fix ownership for deploy dir and repo build artifacts
echo "[6/7] Fixing permissions..."
chown -R "$SERVICE_USER:$SERVICE_USER" "$DEPLOY_DIR"
# Revert repo build artifacts back to the original user to avoid "access denied" on subsequent non-sudo builds
ORIGINAL_USER="${SUDO_USER:-$(who am i | awk '{print $1}')}"
if [ -n "$ORIGINAL_USER" ] && [ "$ORIGINAL_USER" != "root" ]; then
    chown -R "$ORIGINAL_USER:$ORIGINAL_USER" "$REPO_ROOT/src" "$REPO_ROOT/tests" "$REPO_ROOT/tools" 2>/dev/null || true
fi

# Start the service
echo "[7/7] Starting service..."
systemctl start dotnetcloud

echo "=== Deploy complete ==="

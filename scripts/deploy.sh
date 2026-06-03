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
echo "[1/5] Stopping service..."
systemctl stop dotnetcloud 2>/dev/null || true

# Deploy Core.Server
echo "[2/5] Publishing Core.Server..."
dotnet publish "$REPO_ROOT/src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj" \
    -c "$CONFIG" -o "$DEPLOY_DIR" --no-self-contained \
    /p:DebugType=None /p:DebugSymbols=false

# Deploy module hosts
echo "[3/5] Publishing module hosts..."
for module in Contacts Calendar; do
    module_lower=$(echo "$module" | tr '[:upper:]' '[:lower:]')
    dotnet publish "$REPO_ROOT/src/Modules/$module/DotNetCloud.Modules.$module.Host/DotNetCloud.Modules.$module.Host.csproj" \
        -c "$CONFIG" -o "$MODULES_DIR/dotnetcloud.$module_lower" --no-self-contained \
        /p:DebugType=None /p:DebugSymbols=false
done

# Fix ownership
echo "[4/5] Fixing permissions..."
chown -R "$SERVICE_USER:$SERVICE_USER" "$DEPLOY_DIR"

# Start the service
echo "[5/5] Starting service..."
systemctl start dotnetcloud

echo "=== Deploy complete ==="

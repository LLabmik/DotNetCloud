#!/usr/bin/env bash
# DotNetCloud bare-metal redeploy helper.
# Publishes the server, restarts dotnetcloud.service, and verifies /health/live.

set -euo pipefail

SERVICE_NAME="${SERVICE_NAME:-dotnetcloud.service}"
PROJECT_PATH="${PROJECT_PATH:-src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj}"
OUTPUT_DIR="${OUTPUT_DIR:-artifacts/publish/server-baremetal}"
CONFIGURATION="${CONFIGURATION:-Release}"
HEALTH_URL="${HEALTH_URL:-}"
HEALTH_RETRIES="${HEALTH_RETRIES:-15}"
HEALTH_DELAY_SECONDS="${HEALTH_DELAY_SECONDS:-2}"

info() { printf '[INFO] %s\n' "$*"; }
error() { printf '[ERROR] %s\n' "$*" >&2; }

usage() {
    cat <<'EOF'
Usage:
  ./tools/redeploy-baremetal.sh

Environment overrides:
  SERVICE_NAME         systemd unit name (default: dotnetcloud.service)
  PROJECT_PATH         server csproj path (default: src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj)
  OUTPUT_DIR           publish output directory (default: artifacts/publish/server-baremetal)
  CONFIGURATION        dotnet publish configuration (default: Release)
    HEALTH_URL           single liveness URL override (default: auto-try https://localhost:15443/health/live then http://localhost:5080/health/live)
  HEALTH_RETRIES       retry attempts for health probe (default: 15)
  HEALTH_DELAY_SECONDS delay between retries (default: 2)
EOF
}

if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
    error "dotnet is required but not found on PATH."
    exit 1
fi

if ! command -v systemctl >/dev/null 2>&1; then
    error "systemctl is required but not found on PATH."
    exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
    error "curl is required but not found on PATH."
    exit 1
fi

if [[ ! -f "$PROJECT_PATH" ]]; then
    error "Server project not found: $PROJECT_PATH"
    exit 1
fi

info "Publishing server to $OUTPUT_DIR..."
dotnet publish "$PROJECT_PATH" --configuration "$CONFIGURATION" --output "$OUTPUT_DIR"

info "Restarting $SERVICE_NAME..."
if systemctl restart "$SERVICE_NAME" 2>/dev/null; then
    :
elif sudo -n systemctl restart "$SERVICE_NAME" 2>/dev/null; then
    :
else
    error "Failed to restart $SERVICE_NAME (requires systemd restart permission)."
    exit 1
fi

info "Checking service status..."
if ! systemctl status "$SERVICE_NAME" --no-pager >/dev/null 2>&1; then
    error "$SERVICE_NAME is not active after restart."
    systemctl status "$SERVICE_NAME" --no-pager || true
    exit 1
fi

health_urls=()
if [[ -n "$HEALTH_URL" ]]; then
    health_urls+=("$HEALTH_URL")
else
    # Keep parity with both local source deployments (HTTPS) and install.sh defaults (HTTP).
    health_urls+=("https://localhost:15443/health/live")
    health_urls+=("http://localhost:5080/health/live")
fi

info "Probing health endpoint(s): ${health_urls[*]}"
for ((attempt = 1; attempt <= HEALTH_RETRIES; attempt++)); do
    for url in "${health_urls[@]}"; do
        if health_json="$(curl -kfsS "$url" 2>/dev/null)"; then
            printf '%s\n' "$health_json"
            info "Healthy endpoint: $url"
            info "Redeploy complete."
            exit 0
        fi
    done

    if (( attempt < HEALTH_RETRIES )); then
        sleep "$HEALTH_DELAY_SECONDS"
    fi

done

error "Health probe failed after $HEALTH_RETRIES attempts for endpoint(s): ${health_urls[*]}"
systemctl status "$SERVICE_NAME" --no-pager || true
exit 1

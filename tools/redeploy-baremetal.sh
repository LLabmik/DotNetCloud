#!/usr/bin/env bash
# DotNetCloud bare-metal redeploy helper.
# Publishes the server AND CLI, restarts dotnetcloud.service, and verifies /health/live.
# The CLI sets env vars (Collabora proxy config, etc.) that the server reads at startup,
# so both must be deployed together.

set -euo pipefail

SERVICE_NAME="${SERVICE_NAME:-dotnetcloud.service}"
SOLUTION_FILTER="${SOLUTION_FILTER:-DotNetCloud.CI.slnf}"
PROJECT_PATH="${PROJECT_PATH:-src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj}"
CLI_PROJECT_PATH="${CLI_PROJECT_PATH:-src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj}"
OUTPUT_DIR="${OUTPUT_DIR:-artifacts/publish/server-baremetal}"
CLI_OUTPUT_DIR="${CLI_OUTPUT_DIR:-artifacts/publish/cli-linux-x64}"
CONFIGURATION="${CONFIGURATION:-Release}"
HEALTH_URL="${HEALTH_URL:-}"
HEALTH_RETRIES="${HEALTH_RETRIES:-15}"
HEALTH_DELAY_SECONDS="${HEALTH_DELAY_SECONDS:-2}"

info() { printf '[INFO] %s\n' "$*"; }
error() { printf '[ERROR] %s\n' "$*" >&2; }

repair_build_output_ownership() {
    local current_user current_group
    current_user="$(id -un)"
    current_group="$(id -gn)"

    local build_dirs=()
    mapfile -t build_dirs < <(find src tests -type d \( -name bin -o -name obj \) -prune -print 2>/dev/null || true)
    build_dirs+=("artifacts/publish")

    for dir in "${build_dirs[@]}"; do
        [[ -e "$dir" ]] || continue

        if find "$dir" \( ! -user "$current_user" -o ! -group "$current_group" \) -print -quit | grep -q .; then
            info "Repairing build output ownership in $dir..."
            sudo chown -R "$current_user:$current_group" "$dir"
        fi
    done
}

purge_conflicting_build_outputs() {
    local conflicting_configuration=""

    if [[ "$CONFIGURATION" != "Debug" ]]; then
        conflicting_configuration="Debug"
    fi

    [[ -n "$conflicting_configuration" ]] || return

    local stale_dirs=()
    local windows_style_dirs=()

    mapfile -t stale_dirs < <(find src tests -type d \( -path "*/bin/$conflicting_configuration" -o -path "*/obj/$conflicting_configuration" \) -prune -print 2>/dev/null || true)
    mapfile -t windows_style_dirs < <(find src tests -type d \( -path "*bin\\\\$conflicting_configuration" -o -path "*obj\\\\$conflicting_configuration" \) -prune -print 2>/dev/null || true)
    stale_dirs+=("${windows_style_dirs[@]}")

    if [[ ${#stale_dirs[@]} -eq 0 ]]; then
        return
    fi

    info "Removing stale $conflicting_configuration outputs that can conflict with $CONFIGURATION publish..."
    rm -rf "${stale_dirs[@]}"
}

usage() {
    cat <<'EOF'
Usage:
  ./tools/redeploy-baremetal.sh

Environment overrides:
  SERVICE_NAME         systemd unit name (default: dotnetcloud.service)
  SOLUTION_FILTER      solution filter for build (default: DotNetCloud.CI.slnf)
  PROJECT_PATH         server csproj path (default: src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj)
  CLI_PROJECT_PATH     CLI csproj path (default: src/CLI/DotNetCloud.CLI/DotNetCloud.CLI.csproj)
  OUTPUT_DIR           server publish output directory (default: artifacts/publish/server-baremetal)
  CLI_OUTPUT_DIR       CLI publish output directory (default: artifacts/publish/cli-linux-x64)
  CONFIGURATION        dotnet publish configuration (default: Release)
  HEALTH_URL           single liveness URL override (default: auto-try https://localhost:5443/health/live then http://localhost:5080/health/live)
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

if [[ ! -f "$CLI_PROJECT_PATH" ]]; then
    error "CLI project not found: $CLI_PROJECT_PATH"
    exit 1
fi

# Acquire sudo upfront so later commands don't prompt mid-deploy.
sudo -v || { error "sudo authentication failed."; exit 1; }

# Previous root-run builds can leave root-owned bin/obj/publish artifacts behind,
# which causes later non-root StaticWebAssets writes to fail with access denied.
repair_build_output_ownership

# A prior Debug build can leave host/runtime artifacts in bin/obj that collide with
# the server's Release publish graph when publish runs with --no-build.
purge_conflicting_build_outputs

# Build via the CI solution filter to avoid restoring/building Android/MAUI projects.
info "Building $SOLUTION_FILTER ($CONFIGURATION)..."
dotnet build "$SOLUTION_FILTER" --configuration "$CONFIGURATION"

info "Publishing server to $OUTPUT_DIR..."
dotnet publish "$PROJECT_PATH" --configuration "$CONFIGURATION" --no-build --output "$OUTPUT_DIR"

info "Publishing CLI to $CLI_OUTPUT_DIR (framework-dependent, portable)..."
dotnet publish "$CLI_PROJECT_PATH" --configuration "$CONFIGURATION" \
    --no-build --self-contained false --output "$CLI_OUTPUT_DIR"

# Copy published output to the installed locations.
# The systemd service runs /opt/dotnetcloud/dotnetcloud start which:
#   1. The CLI at /opt/dotnetcloud/cli/ reads config and sets env vars
#   2. Then launches the server from /opt/dotnetcloud/cli/server/
INSTALL_CLI_ROOT="/opt/dotnetcloud/cli"
INSTALL_CLI_DIR="/opt/dotnetcloud/cli/server"
INSTALL_SERVER_DIR="/opt/dotnetcloud/server"

stop_service() {
    sudo systemctl stop "$SERVICE_NAME"
}

start_service() {
    sudo systemctl start "$SERVICE_NAME"
}

info "Stopping $SERVICE_NAME..."
if ! stop_service; then
    error "Failed to stop $SERVICE_NAME (requires systemd permission)."
    exit 1
fi

# Deploy CLI (framework-dependent binary that sets Collabora proxy config, etc.)
if [[ -d "$INSTALL_CLI_ROOT" ]]; then
    info "Deploying CLI to $INSTALL_CLI_ROOT..."
    # Clean stale files (e.g., leftover runtime DLLs from previous self-contained
    # publish) but preserve the server/ subdirectory which is deployed separately.
    find "$INSTALL_CLI_ROOT" -maxdepth 1 -type f -exec sudo rm -f {} +
    sudo cp -r "$CLI_OUTPUT_DIR"/* "$INSTALL_CLI_ROOT/"
fi

# Deploy server
if [[ -d "$INSTALL_CLI_DIR" ]]; then
    info "Deploying server to $INSTALL_CLI_DIR..."
    sudo cp -r "$OUTPUT_DIR"/* "$INSTALL_CLI_DIR/"
fi

if [[ -d "$INSTALL_SERVER_DIR" ]]; then
    info "Deploying server to $INSTALL_SERVER_DIR..."
    sudo cp -r "$OUTPUT_DIR"/* "$INSTALL_SERVER_DIR/"
fi

info "Starting $SERVICE_NAME..."
if ! start_service; then
    error "Failed to start $SERVICE_NAME (requires systemd permission)."
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
    health_urls+=("https://localhost:5443/health/live")
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

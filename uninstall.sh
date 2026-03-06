#!/usr/bin/env bash
# DotNetCloud pre-install cleanup script.
# Runs steps 1-3 from the fresh-install prep checklist:
# 1) Stop running DotNetCloud processes/services
# 2) Backup current install/config/data
# 3) Remove installed DotNetCloud footprint
#
# This script intentionally DOES NOT reinstall DotNetCloud.

set -euo pipefail

DRY_RUN=false
if [[ "${1:-}" == "--dry-run" ]]; then
    DRY_RUN=true
fi

run_cmd() {
    if [[ "$DRY_RUN" == "true" ]]; then
        echo "[DRY-RUN] $*"
    else
        "$@"
    fi
}

if [[ ${EUID:-$(id -u)} -ne 0 ]]; then
    if command -v sudo >/dev/null 2>&1; then
        SUDO="sudo"
    else
        echo "[ERROR] Run as root or install sudo." >&2
        exit 1
    fi
else
    SUDO=""
fi

TS="$(date +%Y%m%d-%H%M%S)"
BACKUP_DIR="${HOME}/dotnetcloud-backup-${TS}"

echo "[INFO] Step 1/3: Stopping running DotNetCloud processes/services..."
run_cmd pkill -f "dotnet run --project src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj" || true
if [[ -n "$SUDO" ]]; then
    run_cmd sudo systemctl stop dotnetcloud.service || true
else
    run_cmd systemctl stop dotnetcloud.service || true
fi

echo "[INFO] Step 2/3: Backing up current install/config/data to ${BACKUP_DIR}..."
run_cmd mkdir -p "${BACKUP_DIR}"
if [[ -n "$SUDO" ]]; then
    run_cmd sudo cp -a /etc/dotnetcloud "${BACKUP_DIR}/" || true
    run_cmd sudo cp -a /opt/dotnetcloud "${BACKUP_DIR}/" || true
    run_cmd sudo cp -a /var/lib/dotnetcloud "${BACKUP_DIR}/" || true
    run_cmd sudo cp -a /var/log/dotnetcloud "${BACKUP_DIR}/" || true
else
    run_cmd cp -a /etc/dotnetcloud "${BACKUP_DIR}/" || true
    run_cmd cp -a /opt/dotnetcloud "${BACKUP_DIR}/" || true
    run_cmd cp -a /var/lib/dotnetcloud "${BACKUP_DIR}/" || true
    run_cmd cp -a /var/log/dotnetcloud "${BACKUP_DIR}/" || true
fi

echo "[INFO] Step 3/3: Removing installed DotNetCloud footprint..."
if [[ -n "$SUDO" ]]; then
    run_cmd sudo systemctl disable dotnetcloud.service || true
    run_cmd sudo rm -f /etc/systemd/system/dotnetcloud.service
    run_cmd sudo systemctl daemon-reload
    run_cmd sudo rm -rf /opt/dotnetcloud /etc/dotnetcloud /var/lib/dotnetcloud /var/log/dotnetcloud /run/dotnetcloud
    run_cmd sudo rm -f /usr/local/bin/dotnetcloud
else
    run_cmd systemctl disable dotnetcloud.service || true
    run_cmd rm -f /etc/systemd/system/dotnetcloud.service
    run_cmd systemctl daemon-reload
    run_cmd rm -rf /opt/dotnetcloud /etc/dotnetcloud /var/lib/dotnetcloud /var/log/dotnetcloud /run/dotnetcloud
    run_cmd rm -f /usr/local/bin/dotnetcloud
fi

echo ""
echo "[OK] Cleanup complete."
if [[ "$DRY_RUN" == "true" ]]; then
    echo "[INFO] Dry run only. No changes were made."
fi
echo "[INFO] Backup directory: ${BACKUP_DIR}"
echo "[INFO] Next: pull latest from GitHub, then run install.sh when ready."

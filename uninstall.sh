#!/usr/bin/env bash
# DotNetCloud pre-install cleanup script.
# Runs steps 1-3 from the fresh-install prep checklist:
# 1) Stop running DotNetCloud processes/services
# 2) Backup current install/config/data
# 2b) Optionally drop the DotNetCloud database (prompt, default NO)
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

# --- Locate the persisted runtime config (provider + connection string) ---
find_uninstall_config() {
    local config_file="/etc/dotnetcloud/config.json"
    if [[ -f "$config_file" ]]; then
        echo "$config_file"
        return 0
    fi
    config_file="/root/.config/dotnetcloud/config.json"
    if [[ -f "$config_file" ]]; then
        echo "$config_file"
        return 0
    fi
    return 1
}

# --- Extract a single key=value from a provider connection string ---
conn_value() {
    local conn="$1"
    local key="$2"
    printf '%s' "$conn" | grep -ioP "${key}\s*=\s*\K[^;]+" 2>/dev/null | head -n1 \
        | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' || true
}

# --- Run a command as the postgres system user (root or sudo) ---
run_as_postgres() {
    if [[ -n "$SUDO" ]]; then
        run_cmd sudo -u postgres "$@"
    elif command -v runuser >/dev/null 2>&1; then
        run_cmd runuser -u postgres -- "$@"
    else
        run_cmd sudo -u postgres "$@"
    fi
}

# --- Best-effort SQL Server database drop via sqlcmd ---
drop_sqlserver_database() {
    local conn="$1"
    local db_name="$2"

    if ! command -v sqlcmd >/dev/null 2>&1; then
        echo "[WARN] sqlcmd not found — SQL Server database '${db_name}' left in place."
        return 1
    fi

    local server user password
    server=$(conn_value "$conn" "Server")
    user=$(conn_value "$conn" "User Id")
    password=$(conn_value "$conn" "Password")

    local sql="DROP DATABASE IF EXISTS [${db_name}]"

    if [[ -n "$user" && -n "$password" ]]; then
        run_cmd sqlcmd -S "$server" -U "$user" -P "$password" -C -b -Q "$sql"
    else
        run_cmd sqlcmd -S "$server" -E -C -b -Q "$sql"
    fi
}

# --- Ask whether to drop the DotNetCloud database (default: keep data) ---
maybe_drop_database() {
    if [[ "$DRY_RUN" == "true" ]]; then
        return
    fi

    local config_file
    config_file=$(find_uninstall_config) || return

    local provider
    provider=$(grep -oP '"(?:databaseProvider|DatabaseProvider)"\s*:\s*"\K[^"]+' "$config_file" 2>/dev/null | head -n1 || true)
    if [[ -z "$provider" ]]; then
        # Nested canonical form: "database": { "provider": "..." }
        provider=$(grep -oP '"provider"\s*:\s*"\K[^"]+' "$config_file" 2>/dev/null | head -n1 || true)
    fi

    local conn
    conn=$(grep -oP '"(?:connectionString|ConnectionString)"\s*:\s*"\K[^"]+' "$config_file" 2>/dev/null | head -n1 || true)

    if [[ -z "$provider" || -z "$conn" ]]; then
        return
    fi

    local db_name db_user
    db_name=$(conn_value "$conn" "Database")
    if [[ -z "$db_name" ]]; then
        db_name=$(conn_value "$conn" "Initial Catalog")
    fi
    if [[ -z "$db_name" ]]; then
        return
    fi
    if [[ ! "$db_name" =~ ^[A-Za-z0-9_]+$ ]]; then
        echo "[WARN] Skipping database drop — unexpected database name '${db_name}'."
        return
    fi

    if [[ "${provider,,}" == "postgresql" ]]; then
        db_user=$(conn_value "$conn" "Username")
    else
        db_user=$(conn_value "$conn" "User Id")
    fi

    # Default to NO (data-preserving) when stdin is not a terminal.
    if [[ ! -t 0 ]]; then
        echo "[INFO] Non-interactive run — database '${db_name}' left in place."
        return
    fi

    local response
    echo ""
    if [[ -n "$db_user" ]]; then
        echo "[INFO] DotNetCloud database: '${db_name}' (provider: ${provider}, user: ${db_user})"
    else
        echo "[INFO] DotNetCloud database: '${db_name}' (provider: ${provider})"
    fi
    read -r -p "Drop the DotNetCloud database '${db_name}'? [y/N] " response

    if [[ "$response" != "y" && "$response" != "Y" ]]; then
        echo "[INFO] Database '${db_name}' left in place."
        return
    fi

    case "${provider,,}" in
        postgresql|postgres)
            echo "[INFO] Dropping PostgreSQL database '${db_name}'..."
            if run_as_postgres psql -c "DROP DATABASE IF EXISTS \"${db_name}\" WITH (FORCE);"; then
                echo "[OK] Database '${db_name}' dropped."
            else
                echo "[WARN] Could not drop database '${db_name}' (check PostgreSQL is running and you have permissions)."
            fi
            ;;
        sqlserver|mssql)
            echo "[INFO] Dropping SQL Server database '${db_name}'..."
            if drop_sqlserver_database "$conn" "$db_name"; then
                echo "[OK] Database '${db_name}' dropped."
            else
                echo "[WARN] Could not drop database '${db_name}'."
            fi
            ;;
        *)
            echo "[WARN] Unknown database provider '${provider}' — database '${db_name}' left in place."
            ;;
    esac
}

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

# Optional: prompt to drop the DotNetCloud database (default: keep data).
maybe_drop_database

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

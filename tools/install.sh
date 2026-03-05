#!/usr/bin/env bash
# DotNetCloud — Linux Install & Upgrade Script
# Usage: curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash
#
# This script installs or upgrades DotNetCloud on Debian-based Linux distributions.
# It detects existing installations and handles upgrades safely:
#   - Stops the running service before replacing binaries
#   - Cleans stale files from previous versions
#   - Runs database migrations after upgrade
#   - Restarts the service automatically
#
# Requirements:
#   - Debian-based Linux (Ubuntu 22.04+, Debian 12+, Linux Mint 21+)
#   - curl or wget
#   - Root or sudo access
#
# License: AGPL-3.0 — https://github.com/LLabmik/DotNetCloud

set -euo pipefail

# --- Configuration ---
REPO="LLabmik/DotNetCloud"
INSTALL_DIR="/opt/dotnetcloud"
CONFIG_DIR="/etc/dotnetcloud"
DATA_DIR="/var/lib/dotnetcloud"
LOG_DIR="/var/log/dotnetcloud"
RUN_DIR="/run/dotnetcloud"
SERVICE_USER="dotnetcloud"
SERVICE_GROUP="dotnetcloud"

# --- State (set during execution) ---
IS_UPGRADE=false
INSTALLED_VERSION=""
LATEST_VERSION=""

# --- Colors ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

info()  { echo -e "${BLUE}[INFO]${NC} $*"; }
ok()    { echo -e "${GREEN}[OK]${NC} $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $*"; }
error() { echo -e "${RED}[ERROR]${NC} $*" >&2; }
fatal() { error "$*"; exit 1; }

# --- Pre-flight checks ---
check_prerequisites() {
    info "Checking prerequisites..."

    # Must be Linux
    if [[ "$(uname -s)" != "Linux" ]]; then
        fatal "This installer only supports Linux. For Windows, see: https://github.com/${REPO}#windows"
    fi

    # Must be root or have sudo
    if [[ $EUID -ne 0 ]]; then
        if command -v sudo &>/dev/null; then
            warn "Not running as root. Will use sudo for privileged operations."
            SUDO="sudo"
        else
            fatal "This script must be run as root or with sudo available."
        fi
    else
        SUDO=""
    fi

    # Check for Debian-based distro
    if [[ ! -f /etc/debian_version ]] && [[ ! -f /etc/lsb-release ]]; then
        warn "This installer is designed for Debian-based distributions (Ubuntu, Debian, Linux Mint)."
        warn "It may work on other distributions but is not tested."
    fi

    # Check for required tools
    for cmd in curl tar; do
        if ! command -v "$cmd" &>/dev/null; then
            info "Installing $cmd..."
            $SUDO apt-get update -qq && $SUDO apt-get install -y -qq "$cmd"
        fi
    done

    ok "Prerequisites satisfied."
}

# --- Semantic version comparison ---
# Returns: 0 if equal, 1 if $1 > $2, 2 if $1 < $2
version_compare() {
    if [[ "$1" == "$2" ]]; then
        return 0
    fi

    local IFS=.
    local i ver1=($1) ver2=($2)

    # Pad shorter version with zeros
    for ((i=${#ver1[@]}; i<${#ver2[@]}; i++)); do
        ver1[i]=0
    done
    for ((i=${#ver2[@]}; i<${#ver1[@]}; i++)); do
        ver2[i]=0
    done

    for ((i=0; i<${#ver1[@]}; i++)); do
        # Strip any pre-release suffix for numeric comparison
        local v1="${ver1[i]%%[-+]*}"
        local v2="${ver2[i]%%[-+]*}"
        if ((v1 > v2)); then
            return 1
        fi
        if ((v1 < v2)); then
            return 2
        fi
    done
    return 0
}

# --- Detect existing installation ---
detect_existing_install() {
    if [[ -f "${INSTALL_DIR}/VERSION" ]]; then
        INSTALLED_VERSION=$(cat "${INSTALL_DIR}/VERSION" | tr -d '[:space:]')
        IS_UPGRADE=true
        info "Existing installation detected: v${INSTALLED_VERSION}"
    elif [[ -f "${INSTALL_DIR}/dotnetcloud" ]]; then
        # Binary exists but no VERSION file (pre-VERSION-file install)
        INSTALLED_VERSION="unknown"
        IS_UPGRADE=true
        warn "Existing installation detected (version unknown — no VERSION file)."
    fi
}

# --- Check if upgrade is needed ---
check_version_skip() {
    if [[ "$IS_UPGRADE" == true ]] && [[ "$INSTALLED_VERSION" != "unknown" ]]; then
        set +e
        version_compare "$INSTALLED_VERSION" "$LATEST_VERSION"
        local result=$?
        set -e

        if [[ $result -eq 0 ]]; then
            ok "Already running v${LATEST_VERSION}. Nothing to do."
            exit 0
        elif [[ $result -eq 1 ]]; then
            warn "Installed version (v${INSTALLED_VERSION}) is NEWER than latest release (v${LATEST_VERSION})."
            warn "This would be a downgrade. If intentional, uninstall first:"
            echo "  sudo systemctl stop dotnetcloud"
            echo "  sudo rm -rf ${INSTALL_DIR}"
            echo "  # Then re-run this script"
            exit 1
        fi

        info "Upgrading: v${INSTALLED_VERSION} → v${LATEST_VERSION}"
    fi
}

# --- Stop running service before upgrade ---
stop_existing_service() {
    if [[ "$IS_UPGRADE" != true ]]; then
        return
    fi

    if systemctl is-active --quiet dotnetcloud.service 2>/dev/null; then
        info "Stopping DotNetCloud service..."
        $SUDO systemctl stop dotnetcloud.service
        ok "Service stopped."
    else
        info "Service is not running."
    fi
}

# --- Detect latest release version ---
get_latest_version() {
    info "Checking latest DotNetCloud release..."

    local HTTP_CODE
    local RESPONSE
    RESPONSE=$(curl -sL -w "\n%{http_code}" "https://api.github.com/repos/${REPO}/releases/latest" 2>/dev/null) || true
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    RESPONSE=$(echo "$RESPONSE" | sed '$d')

    if [[ "$HTTP_CODE" == "404" ]]; then
        echo ""
        error "No releases have been published yet."
        error "DotNetCloud is still in early development and has no downloadable release packages."
        echo ""
        info "To install from source instead, run:"
        echo ""
        echo "  git clone https://github.com/${REPO}.git"
        echo "  cd DotNetCloud"
        echo "  dotnet publish src/Core/DotNetCloud.Core.Server/DotNetCloud.Core.Server.csproj \\"
        echo "    --configuration Release --runtime linux-x64 --self-contained true \\"
        echo "    --output /opt/dotnetcloud/server"
        echo ""
        info "See the full guide: https://github.com/${REPO}/blob/main/docs/admin/server/INSTALLATION.md"
        echo ""
        exit 1
    fi

    LATEST_VERSION=$(echo "$RESPONSE" \
        | grep '"tag_name"' \
        | sed -E 's/.*"tag_name":\s*"v?([^"]+)".*/\1/')

    if [[ -z "${LATEST_VERSION:-}" ]]; then
        fatal "Could not determine latest version. Check https://github.com/${REPO}/releases"
    fi

    ok "Latest version: ${LATEST_VERSION}"
}

# --- Download and install ---
install_dotnetcloud() {
    local ARCHIVE_NAME="dotnetcloud-${LATEST_VERSION}-linux-x64.tar.gz"
    local ARCHIVE_URL="https://github.com/${REPO}/releases/download/v${LATEST_VERSION}/${ARCHIVE_NAME}"
    local CHECKSUM_URL="${ARCHIVE_URL}.sha256"
    local TEMP_FILE="/tmp/${ARCHIVE_NAME}"
    local TEMP_CHECKSUM="/tmp/${ARCHIVE_NAME}.sha256"

    info "Downloading DotNetCloud v${LATEST_VERSION}..."
    curl -fSL "$ARCHIVE_URL" -o "$TEMP_FILE" || fatal "Download failed. Is v${LATEST_VERSION} published at ${ARCHIVE_URL}?"

    info "Verifying checksum..."
    if curl -fSL "$CHECKSUM_URL" -o "$TEMP_CHECKSUM" 2>/dev/null; then
        (cd /tmp && sha256sum --check "$TEMP_CHECKSUM") || fatal "Checksum verification failed! The download may be corrupted."
        ok "Checksum verified."
        rm -f "$TEMP_CHECKSUM"
    else
        warn "Checksum file not available. Skipping verification."
    fi

    info "Creating service user..."
    if ! id "$SERVICE_USER" &>/dev/null; then
        $SUDO useradd --system --shell /usr/sbin/nologin --home-dir "$DATA_DIR" "$SERVICE_USER"
    fi

    info "Creating directory structure..."
    $SUDO mkdir -p "$INSTALL_DIR" "$CONFIG_DIR" "$DATA_DIR" "$LOG_DIR" "$RUN_DIR"
    $SUDO mkdir -p "${DATA_DIR}/files"

    # On upgrade: remove old binaries to prevent stale files
    if [[ "$IS_UPGRADE" == true ]]; then
        info "Removing old binaries (config and data are preserved)..."
        # Remove old binary directories but preserve VERSION for rollback reference
        $SUDO rm -rf "${INSTALL_DIR}/server" "${INSTALL_DIR}/modules"
        # Remove old top-level binaries (CLI, DLLs, etc.) but not directories we just cleaned
        $SUDO find "$INSTALL_DIR" -maxdepth 1 -type f -delete
    fi

    info "Extracting to ${INSTALL_DIR}..."
    $SUDO tar -xzf "$TEMP_FILE" -C "$INSTALL_DIR" --strip-components=1

    info "Setting permissions..."
    $SUDO chown -R "${SERVICE_USER}:${SERVICE_GROUP}" "$DATA_DIR" "$LOG_DIR" "$RUN_DIR"
    $SUDO chown -R root:root "$INSTALL_DIR"
    $SUDO chmod 755 "$INSTALL_DIR"

    # Symlink CLI to PATH
    $SUDO ln -sf "${INSTALL_DIR}/dotnetcloud" /usr/local/bin/dotnetcloud

    rm -f "$TEMP_FILE"

    ok "DotNetCloud v${LATEST_VERSION} installed to ${INSTALL_DIR}"
}

# --- Create systemd service ---
install_service() {
    info "Installing systemd service..."

    $SUDO tee /etc/systemd/system/dotnetcloud.service > /dev/null <<EOF
[Unit]
Description=DotNetCloud Core Server
Documentation=https://github.com/LLabmik/DotNetCloud
After=network.target postgresql.service
Requires=network.target

[Service]
Type=notify
User=${SERVICE_USER}
Group=${SERVICE_GROUP}
WorkingDirectory=${INSTALL_DIR}
ExecStart=${INSTALL_DIR}/dotnetcloud serve
ExecStop=${INSTALL_DIR}/dotnetcloud stop
Restart=on-failure
RestartSec=10
TimeoutStartSec=60
TimeoutStopSec=30

# Security hardening
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=${DATA_DIR} ${LOG_DIR} ${RUN_DIR} ${CONFIG_DIR}
PrivateTmp=true

# Environment
Environment=DOTNET_ENVIRONMENT=Production
Environment=DOTNETCLOUD_CONFIG_DIR=${CONFIG_DIR}
Environment=DOTNETCLOUD_DATA_DIR=${DATA_DIR}
Environment=DOTNETCLOUD_LOG_DIR=${LOG_DIR}

[Install]
WantedBy=multi-user.target
EOF

    $SUDO systemctl daemon-reload

    # Only enable auto-start if this is an upgrade (config already exists).
    # Fresh installs must run 'dotnetcloud setup' first.
    if [[ "$IS_UPGRADE" == true ]]; then
        $SUDO systemctl enable dotnetcloud.service
    fi

    ok "Systemd service installed."
}

# --- Post-upgrade: migrate database and restart ---
post_upgrade() {
    if [[ "$IS_UPGRADE" != true ]]; then
        return
    fi

    info "Running database migrations..."
    if $SUDO -u "$SERVICE_USER" "${INSTALL_DIR}/dotnetcloud" setup --migrate-only 2>/dev/null; then
        ok "Database migrations complete."
    else
        warn "Database migration skipped (--migrate-only not yet implemented or no migrations needed)."
        warn "Migrations will run automatically on next startup."
    fi

    info "Starting DotNetCloud service..."
    $SUDO systemctl start dotnetcloud.service
    ok "Service started."
}

# --- Install or upgrade Collabora CODE via APT ---
install_collabora() {
    local KEYRING_PATH="/usr/share/keyrings/collaboraonline-release-keyring.gpg"
    local SOURCES_PATH="/etc/apt/sources.list.d/collaboraonline.sources"

    # Ensure the Collabora APT repository is configured
    if [[ ! -f "$SOURCES_PATH" ]]; then
        info "Importing Collabora signing key..."
        curl -fsSL "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x0C54D189F4BA284D" \
            | $SUDO gpg --dearmor -o "$KEYRING_PATH" 2>/dev/null \
            || { error "Failed to import Collabora signing key."; return 1; }

        $SUDO tee "$SOURCES_PATH" > /dev/null <<COLEOF
Types: deb
URIs: https://www.collaboraoffice.com/repos/CollaboraOnline/CODE-deb
Suites: ./
Signed-By: ${KEYRING_PATH}
COLEOF
        info "Collabora APT repository added."
    fi

    # apt-get install is idempotent: installs if missing, upgrades if newer available, no-op if current
    info "Checking Collabora CODE packages (install/upgrade as needed)..."
    $SUDO apt-get update -qq -o Dir::Etc::sourcelist="$SOURCES_PATH" -o Dir::Etc::sourceparts="-" 2>/dev/null \
        || $SUDO apt-get update -qq

    local BEFORE_VERSION=""
    if dpkg -s coolwsd &>/dev/null; then
        BEFORE_VERSION=$(dpkg-query -W -f='${Version}' coolwsd 2>/dev/null || true)
    fi

    $SUDO apt-get install -y -qq coolwsd code-brand \
        || { error "Failed to install Collabora CODE packages."; return 1; }

    local AFTER_VERSION
    AFTER_VERSION=$(dpkg-query -W -f='${Version}' coolwsd 2>/dev/null || true)

    if [[ -z "$BEFORE_VERSION" ]]; then
        ok "Collabora CODE v${AFTER_VERSION} installed."
    elif [[ "$BEFORE_VERSION" == "$AFTER_VERSION" ]]; then
        ok "Collabora CODE v${AFTER_VERSION} is already the latest version."
    else
        ok "Collabora CODE upgraded: v${BEFORE_VERSION} → v${AFTER_VERSION}"
    fi

    # Disable the default systemd service — DotNetCloud's process supervisor manages Collabora
    $SUDO systemctl stop coolwsd 2>/dev/null || true
    $SUDO systemctl disable coolwsd 2>/dev/null || true
}

# --- Check config for Collabora and install/upgrade if requested ---
maybe_install_collabora() {
    local CONFIG_FILE="${CONFIG_DIR}/config.json"

    # Also check the user-level config path used by the CLI
    if [[ ! -f "$CONFIG_FILE" ]]; then
        CONFIG_FILE="/root/.config/dotnetcloud/config.json"
    fi

    if [[ ! -f "$CONFIG_FILE" ]]; then
        return
    fi

    # Read CollaboraMode from config (simple grep — no jq dependency)
    local MODE
    MODE=$(grep -oP '"CollaboraMode"\s*:\s*"\K[^"]+' "$CONFIG_FILE" 2>/dev/null || true)

    if [[ "$MODE" == "BuiltIn" ]]; then
        install_collabora
    fi
}

# --- Main ---
main() {
    echo ""
    echo -e "${BLUE}╔══════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║     DotNetCloud Linux Installer      ║${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════╝${NC}"
    echo ""

    check_prerequisites
    detect_existing_install
    get_latest_version
    check_version_skip

    if [[ "$IS_UPGRADE" == true ]]; then
        info "Upgrade mode: v${INSTALLED_VERSION} → v${LATEST_VERSION}"
        stop_existing_service
        install_dotnetcloud
        install_service
        post_upgrade
        maybe_install_collabora

        echo ""
        ok "Upgrade complete! v${INSTALLED_VERSION} → v${LATEST_VERSION}"
        echo ""
        info "Verify the upgrade:"
        echo "  dotnetcloud --version"
        echo "  sudo systemctl status dotnetcloud"
        echo "  curl -s http://localhost:5080/health"
        echo ""
    else
        install_dotnetcloud
        install_service

        echo ""
        ok "Installation complete!"
        echo ""

        # Offer to run setup immediately for a seamless experience
        read -rp "$(echo -e "${BLUE}[INFO]${NC} Run the setup wizard now? [Y/n]: ")" RUN_SETUP
        RUN_SETUP="${RUN_SETUP:-Y}"

        if [[ "${RUN_SETUP,,}" == "y" ]]; then
            echo ""
            $SUDO "${INSTALL_DIR}/dotnetcloud" setup
            SETUP_EXIT=$?

            if [[ $SETUP_EXIT -eq 0 ]]; then
                echo ""
                maybe_install_collabora
                echo ""
                info "Starting DotNetCloud service..."
                $SUDO systemctl enable dotnetcloud.service
                $SUDO systemctl start dotnetcloud.service
                ok "Service started and enabled on boot."
                echo ""
                info "Verify:"
                echo "  sudo systemctl status dotnetcloud"
                echo "  curl -s http://localhost:5080/health"
            else
                warn "Setup did not complete. You can run it later:"
                echo "  sudo dotnetcloud setup"
                echo "  sudo systemctl start dotnetcloud"
                echo "  sudo systemctl enable dotnetcloud"
            fi
        else
            echo ""
            info "Run these when you're ready:"
            echo "  1. sudo dotnetcloud setup"
            echo "  2. sudo systemctl start dotnetcloud"
            echo "  3. sudo systemctl enable dotnetcloud"
        fi
        echo ""
    fi

    info "Documentation: https://github.com/${REPO}"
    echo ""
}

main "$@"

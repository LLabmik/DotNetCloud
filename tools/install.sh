#!/usr/bin/env bash
# DotNetCloud — Linux Install Script
# Usage: curl -fsSL https://raw.githubusercontent.com/LLabmik/DotNetCloud/main/tools/install.sh | bash
#
# This script installs DotNetCloud on Debian-based Linux distributions (Ubuntu, Debian, Linux Mint).
# It downloads the latest release from GitHub, installs dependencies, and runs initial setup.
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
    local ARCHIVE_URL="https://github.com/${REPO}/releases/download/v${LATEST_VERSION}/dotnetcloud-${LATEST_VERSION}-linux-x64.tar.gz"
    local TEMP_FILE="/tmp/dotnetcloud-${LATEST_VERSION}.tar.gz"

    info "Downloading DotNetCloud v${LATEST_VERSION}..."
    curl -fSL "$ARCHIVE_URL" -o "$TEMP_FILE" || fatal "Download failed. Is v${LATEST_VERSION} published at ${ARCHIVE_URL}?"

    info "Creating service user..."
    if ! id "$SERVICE_USER" &>/dev/null; then
        $SUDO useradd --system --shell /usr/sbin/nologin --home-dir "$DATA_DIR" "$SERVICE_USER"
    fi

    info "Creating directory structure..."
    $SUDO mkdir -p "$INSTALL_DIR" "$CONFIG_DIR" "$DATA_DIR" "$LOG_DIR" "$RUN_DIR"
    $SUDO mkdir -p "${DATA_DIR}/files"

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
    $SUDO systemctl enable dotnetcloud.service

    ok "Systemd service installed and enabled."
}

# --- Main ---
main() {
    echo ""
    echo -e "${BLUE}╔══════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║     DotNetCloud Linux Installer      ║${NC}"
    echo -e "${BLUE}╚══════════════════════════════════════╝${NC}"
    echo ""

    check_prerequisites
    get_latest_version
    install_dotnetcloud
    install_service

    echo ""
    ok "Installation complete!"
    echo ""
    info "Next steps:"
    echo "  1. Run the setup wizard:    dotnetcloud setup"
    echo "  2. Start the server:        dotnetcloud serve"
    echo "  3. Or start via systemd:    sudo systemctl start dotnetcloud"
    echo ""
    info "Documentation: https://github.com/${REPO}"
    echo ""
}

main "$@"

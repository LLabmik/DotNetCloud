#!/usr/bin/env bash
# =============================================================================
# DotNetCloud — Desktop Client .deb Package Builder
# =============================================================================
# Usage: ./build-desktop-client-deb.sh [VERSION] [CONFIGURATION] [OUTPUT_DIR]
#
# Prerequisites:
#   - .NET SDK installed
#   - dpkg-deb available
#
# Output: $OUTPUT_DIR/dotnetcloud-sync-tray_<version>_amd64.deb
# =============================================================================
set -euo pipefail

VERSION="${1:-0.1.0-alpha}"
CONFIGURATION="${2:-Release}"
OUTPUT_DIR="${3:-./artifacts/installers}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"; pwd)"
SOLUTION_ROOT="$(cd "$SCRIPT_DIR/../.."; pwd)"
SYNCTRAY_PROJECT="$SOLUTION_ROOT/src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj"
DEB_STAGING="$SOLUTION_ROOT/artifacts/deb-staging/desktop-client"
DEB_OUTPUT="$OUTPUT_DIR/dotnetcloud-sync-tray_${VERSION}_amd64.deb"

# Sanitize version for Debian (replace hyphens with tildes: 0.1.0-alpha → 0.1.0~alpha)
DEB_VERSION="${VERSION//-/\~}"

echo "=============================================="
echo " DotNetCloud — Desktop Client .deb Builder"
echo " Version: $VERSION (deb: $DEB_VERSION)"
echo "=============================================="

# Require dpkg-deb
if ! command -v dpkg-deb >/dev/null 2>&1; then
    echo "Error: dpkg-deb is required. Install with: sudo apt install dpkg-dev" >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"

# Clean previous staging
rm -rf "$DEB_STAGING"

# ---- Step 1: Publish self-contained binary ----
echo ""
echo "[1/4] Publishing SyncTray for linux-x64..."
PUBLISH_DIR="$DEB_STAGING/opt/dotnetcloud-desktop-client/SyncTray"
dotnet publish "$SYNCTRAY_PROJECT" \
    --configuration "$CONFIGURATION" \
    --runtime linux-x64 \
    --self-contained true \
    --output "$PUBLISH_DIR"

# ---- Step 2: Create Debian package structure ----
echo "[2/4] Creating Debian package structure..."

DEBIAN_DIR="$DEB_STAGING/DEBIAN"
BIN_DIR="$DEB_STAGING/usr/bin"
APPLICATIONS_DIR="$DEB_STAGING/usr/share/applications"
ICONS_DIR="$DEB_STAGING/usr/share/icons/hicolor/scalable/apps"

mkdir -p "$DEBIAN_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$APPLICATIONS_DIR"
mkdir -p "$ICONS_DIR"

# ---- Step 3: Write package metadata and scripts ----
echo "[3/4] Writing control file and scripts..."

# Compute installed size in KB
INSTALLED_SIZE_KB=$(du -sk "$DEB_STAGING/opt" | awk '{print $1}')

cat > "$DEBIAN_DIR/control" <<EOF
Package: dotnetcloud-sync-tray
Version: $DEB_VERSION
Section: net
Priority: optional
Architecture: amd64
Installed-Size: $INSTALLED_SIZE_KB
Maintainer: DotNetCloud Contributors <noreply@dotnetcloud.dev>
Description: DotNetCloud Sync Client
 Desktop sync tray application for DotNetCloud. Provides file
 synchronization, status monitoring, and system tray integration.
Depends: libc6 (>= 2.31), libssl3 | libssl1.1, libx11-6, libfontconfig1
Homepage: https://github.com/LLabmik/DotNetCloud
EOF

# Launcher wrapper in /usr/bin
cat > "$BIN_DIR/dotnetcloud-sync-tray" <<'EOF'
#!/usr/bin/env bash
exec /opt/dotnetcloud-desktop-client/SyncTray/dotnetcloud-sync-tray "$@"
EOF
chmod 0755 "$BIN_DIR/dotnetcloud-sync-tray"

# Desktop entry
ICON_SRC="$PUBLISH_DIR/Assets/dotnetcloud-sync-cloud.svg"
if [[ -f "$ICON_SRC" ]]; then
    cp "$ICON_SRC" "$ICONS_DIR/dotnetcloud-sync-tray.svg"
    DESKTOP_ICON="dotnetcloud-sync-tray"
else
    DESKTOP_ICON="cloud"
fi

cat > "$APPLICATIONS_DIR/dotnetcloud-sync-tray.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=DotNetCloud Sync Client
Comment=File sync and tray client for DotNetCloud
Exec=/usr/bin/dotnetcloud-sync-tray
Icon=$DESKTOP_ICON
Terminal=false
StartupNotify=true
Categories=Network;Utility;
Keywords=DotNetCloud;Sync;Client;
EOF
chmod 0644 "$APPLICATIONS_DIR/dotnetcloud-sync-tray.desktop"

# postinst — runs after package install
cat > "$DEBIAN_DIR/postinst" <<'EOF'
#!/bin/sh
set -e
chmod 0755 /opt/dotnetcloud-desktop-client/SyncTray/dotnetcloud-sync-tray
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database /usr/share/applications || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache /usr/share/icons/hicolor || true
fi
EOF
chmod 0755 "$DEBIAN_DIR/postinst"

# postrm — runs after package removal
cat > "$DEBIAN_DIR/postrm" <<'EOF'
#!/bin/sh
set -e

# Clean up user-session .desktop files created by the app at runtime
if [ "$1" = "remove" ] || [ "$1" = "purge" ]; then
    for userdir in /home/*; do
        userfile="$userdir/.local/share/applications/dotnetcloud-sync-tray.desktop"
        if [ -f "$userfile" ]; then
            rm -f "$userfile" || true
        fi
    done
fi

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database /usr/share/applications || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache /usr/share/icons/hicolor || true
fi
EOF
chmod 0755 "$DEBIAN_DIR/postrm"

# ---- Step 4: Build .deb ----
echo "[4/4] Building .deb package..."

# Set correct ownership (root:root)
fakeroot dpkg-deb --build "$DEB_STAGING" "$DEB_OUTPUT" 2>/dev/null \
    || dpkg-deb --build "$DEB_STAGING" "$DEB_OUTPUT"

# Generate checksum
DEB_HASH=$(sha256sum "$DEB_OUTPUT" | awk '{print $1}')
DEB_NAME=$(basename "$DEB_OUTPUT")
echo "$DEB_HASH  $DEB_NAME" > "$DEB_OUTPUT.sha256"

DEB_SIZE=$(du -h "$DEB_OUTPUT" | awk '{print $1}')

echo ""
echo "=============================================="
echo " .deb package built successfully!"
echo "=============================================="
echo "  $DEB_OUTPUT ($DEB_SIZE)"
echo "  $DEB_OUTPUT.sha256"
echo ""
echo "Install with:"
echo "  sudo apt install ./$DEB_NAME"
echo ""
echo "Uninstall with:"
echo "  sudo apt remove dotnetcloud-sync-tray"
echo "=============================================="

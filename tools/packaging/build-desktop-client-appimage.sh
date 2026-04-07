#!/usr/bin/env bash
# =============================================================================
# DotNetCloud — Desktop Client AppImage Builder
# =============================================================================
# Usage: ./build-desktop-client-appimage.sh [VERSION] [CONFIGURATION] [OUTPUT_DIR]
#
# Prerequisites:
#   - .NET SDK installed
#   - Downloads appimagetool automatically if not found
#
# Output: $OUTPUT_DIR/DotNetCloud_Sync_Client-<version>-x86_64.AppImage
# =============================================================================
set -euo pipefail

VERSION="${1:-0.1.0-alpha}"
CONFIGURATION="${2:-Release}"
OUTPUT_DIR="${3:-./artifacts/installers}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"; pwd)"
SOLUTION_ROOT="$(cd "$SCRIPT_DIR/../.."; pwd)"
SYNCTRAY_PROJECT="$SOLUTION_ROOT/src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj"
APPDIR="$SOLUTION_ROOT/artifacts/appimage-staging/DotNetCloud_Sync_Client.AppDir"
APPIMAGE_OUTPUT="$OUTPUT_DIR/DotNetCloud_Sync_Client-${VERSION}-x86_64.AppImage"
TOOLS_DIR="$SOLUTION_ROOT/artifacts/appimage-tools"

echo "=============================================="
echo " DotNetCloud — Desktop Client AppImage Builder"
echo " Version: $VERSION"
echo "=============================================="

mkdir -p "$OUTPUT_DIR" "$TOOLS_DIR"

# ---- Ensure appimagetool is available ----
APPIMAGETOOL=""
if command -v appimagetool >/dev/null 2>&1; then
    APPIMAGETOOL="appimagetool"
elif [[ -x "$TOOLS_DIR/appimagetool" ]]; then
    APPIMAGETOOL="$TOOLS_DIR/appimagetool"
else
    echo "[0/4] Downloading appimagetool..."
    APPIMAGETOOL="$TOOLS_DIR/appimagetool"
    curl -fSL "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage" \
        -o "$APPIMAGETOOL"
    chmod +x "$APPIMAGETOOL"
    echo "  Downloaded to $APPIMAGETOOL"
fi

# Verify it works (some systems need --appimage-extract-and-run for FUSE-less builds)
APPIMAGETOOL_RUN="$APPIMAGETOOL"
if ! "$APPIMAGETOOL" --version >/dev/null 2>&1; then
    # FUSE not available — use extract-and-run mode
    APPIMAGETOOL_RUN="$APPIMAGETOOL --appimage-extract-and-run"
fi

# ---- Clean previous staging ----
rm -rf "$APPDIR"

# ---- Step 1: Publish self-contained binary ----
echo ""
echo "[1/4] Publishing SyncTray for linux-x64..."
APP_BIN_DIR="$APPDIR/usr/bin"
APP_LIB_DIR="$APPDIR/usr/lib/dotnetcloud-sync-tray"
mkdir -p "$APP_BIN_DIR" "$APP_LIB_DIR"

dotnet publish "$SYNCTRAY_PROJECT" \
    --configuration "$CONFIGURATION" \
    --runtime linux-x64 \
    --self-contained true \
    --output "$APP_LIB_DIR"

# ---- Step 2: Create AppDir structure ----
echo "[2/4] Creating AppDir structure..."

ICON_SRC="$APP_LIB_DIR/Assets/dotnetcloud-sync-cloud.svg"
ICON_DIR="$APPDIR/usr/share/icons/hicolor/scalable/apps"
mkdir -p "$ICON_DIR"

if [[ -f "$ICON_SRC" ]]; then
    cp "$ICON_SRC" "$ICON_DIR/dotnetcloud-sync-tray.svg"
    cp "$ICON_SRC" "$APPDIR/dotnetcloud-sync-tray.svg"
else
    echo "Warning: SVG icon not found at $ICON_SRC" >&2
fi

# ---- Step 3: Write AppRun, .desktop, and metadata ----
echo "[3/4] Writing AppRun and desktop entry..."

# AppRun — the entry point AppImage executes
cat > "$APPDIR/AppRun" <<'APPRUN_EOF'
#!/usr/bin/env bash
HERE="$(dirname "$(readlink -f "$0")")"
export PATH="$HERE/usr/bin:$PATH"
export LD_LIBRARY_PATH="$HERE/usr/lib/dotnetcloud-sync-tray:${LD_LIBRARY_PATH:-}"

# Detach from terminal so the tray app survives terminal close.
# If stdin is a terminal, launch in background; otherwise run normally
# (e.g. when launched from .desktop file or another process).
if [ -t 0 ]; then
    nohup "$HERE/usr/lib/dotnetcloud-sync-tray/dotnetcloud-sync-tray" "$@" >/dev/null 2>&1 &
    echo "DotNetCloud Sync Client started (PID $!)"
else
    exec "$HERE/usr/lib/dotnetcloud-sync-tray/dotnetcloud-sync-tray" "$@"
fi
APPRUN_EOF
chmod 0755 "$APPDIR/AppRun"

# Wrapper in usr/bin (for PATH if user extracts)
cat > "$APP_BIN_DIR/dotnetcloud-sync-tray" <<'WRAPPER_EOF'
#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"; pwd)"
LIB_DIR="$(cd "$SCRIPT_DIR/../lib/dotnetcloud-sync-tray"; pwd)"
export LD_LIBRARY_PATH="$LIB_DIR:${LD_LIBRARY_PATH:-}"
exec "$LIB_DIR/dotnetcloud-sync-tray" "$@"
WRAPPER_EOF
chmod 0755 "$APP_BIN_DIR/dotnetcloud-sync-tray"

# Ensure the actual binary is executable
chmod 0755 "$APP_LIB_DIR/dotnetcloud-sync-tray"

# Desktop file (required at AppDir root by spec)
cat > "$APPDIR/dotnetcloud-sync-tray.desktop" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=DotNetCloud Sync Client
Comment=File sync and tray client for DotNetCloud
Exec=dotnetcloud-sync-tray
Icon=dotnetcloud-sync-tray
Terminal=false
StartupNotify=true
Categories=Network;Utility;
Keywords=DotNetCloud;Sync;Client;
X-AppImage-Version=$VERSION
EOF

# Also place in standard location
mkdir -p "$APPDIR/usr/share/applications"
cp "$APPDIR/dotnetcloud-sync-tray.desktop" "$APPDIR/usr/share/applications/"

# AppStream metadata (optional but helps with software centers)
METAINFO_DIR="$APPDIR/usr/share/metainfo"
mkdir -p "$METAINFO_DIR"
cat > "$METAINFO_DIR/net.dotnetcloud.sync-tray.metainfo.xml" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>net.dotnetcloud.sync-tray</id>
  <name>DotNetCloud Sync Client</name>
  <summary>File sync and tray client for DotNetCloud</summary>
  <metadata_license>MIT</metadata_license>
  <project_license>Apache-2.0</project_license>
  <launchable type="desktop-id">dotnetcloud-sync-tray.desktop</launchable>
  <description>
    <p>Desktop sync tray application for DotNetCloud. Provides file synchronization,
    status monitoring, and system tray integration for your self-hosted cloud.</p>
  </description>
  <url type="homepage">https://github.com/LLabmik/DotNetCloud</url>
  <developer id="net.dotnetcloud">
    <name>DotNetCloud Contributors</name>
  </developer>
  <content_rating type="oars-1.1"/>
  <provides>
    <binary>dotnetcloud-sync-tray</binary>
  </provides>
  <releases>
    <release version="$VERSION" date="$(date +%Y-%m-%d)"/>
  </releases>
</component>
EOF

# ---- Step 4: Build AppImage ----
echo "[4/4] Building AppImage..."

# Remove the old output if it exists
rm -f "$APPIMAGE_OUTPUT"

# Build with ARCH set
export ARCH=x86_64
$APPIMAGETOOL_RUN "$APPDIR" "$APPIMAGE_OUTPUT" 2>&1 || {
    echo "Retrying with --appimage-extract-and-run..." >&2
    "$APPIMAGETOOL" --appimage-extract-and-run "$APPDIR" "$APPIMAGE_OUTPUT" 2>&1
}

# Generate checksum
APPIMAGE_HASH=$(sha256sum "$APPIMAGE_OUTPUT" | awk '{print $1}')
APPIMAGE_NAME=$(basename "$APPIMAGE_OUTPUT")
echo "$APPIMAGE_HASH  $APPIMAGE_NAME" > "$APPIMAGE_OUTPUT.sha256"

APPIMAGE_SIZE=$(du -h "$APPIMAGE_OUTPUT" | awk '{print $1}')

echo ""
echo "=============================================="
echo " AppImage built successfully!"
echo "=============================================="
echo "  $APPIMAGE_OUTPUT ($APPIMAGE_SIZE)"
echo "  $APPIMAGE_OUTPUT.sha256"
echo ""
echo "Run directly (no install needed):"
echo "  chmod +x $APPIMAGE_NAME && ./$APPIMAGE_NAME"
echo ""
echo "Or move it anywhere and double-click."
echo "=============================================="

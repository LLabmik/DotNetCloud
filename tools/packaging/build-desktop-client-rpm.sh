#!/usr/bin/env bash
# =============================================================================
# DotNetCloud — Desktop Client RPM Package Builder
# =============================================================================
# Usage: ./build-desktop-client-rpm.sh [VERSION] [CONFIGURATION] [OUTPUT_DIR]
#
# Prerequisites:
#   - .NET SDK installed
#   - rpmbuild available (Fedora/RHEL: `sudo dnf install rpm-build`; Ubuntu CI
#     runners: `sudo apt-get install -y rpm`)
#
# Output: $OUTPUT_DIR/dotnetcloud-sync-tray-<version>-1.x86_64.rpm
# =============================================================================
set -euo pipefail

# ── Derive version from latest git tag, fall back to Directory.Build.props ──
_get_version() {
  local repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.."; pwd)"
  local tag="$(git -C "$repo_root" describe --tags --match 'v*' --abbrev=0 2>/dev/null || true)"
  if [[ -n "$tag" ]]; then
    echo "${tag#v}"
    return
  fi
  local props_file="$repo_root/Directory.Build.props"
  local major="$(grep -oP '<MajorVersion>\K[^<]+' "$props_file")"
  local minor="$(grep -oP '<MinorVersion>\K[^<]+' "$props_file")"
  local patch="$(grep -oP '<PatchVersion>\K[^<]+' "$props_file")"
  local prerelease="$(grep -oP '<PreReleaseVersion>\K[^<]+' "$props_file")"
  echo "${major}.${minor}.${patch}-${prerelease}"
}

VERSION="${1:-$(_get_version)}"
CONFIGURATION="${2:-Release}"
OUTPUT_DIR="${3:-./artifacts/installers}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")"; pwd)"
SOLUTION_ROOT="$(cd "$SCRIPT_DIR/../.."; pwd)"
SYNCTRAY_PROJECT="$SOLUTION_ROOT/src/Clients/DotNetCloud.Client.SyncTray/DotNetCloud.Client.SyncTray.csproj"

# Sanitize version for RPM (hyphens are not allowed in the Version tag; mirror
# the .deb convention by mapping them to tildes: 0.1.0-alpha → 0.1.0~alpha)
RPM_VERSION="${VERSION//-/\~}"

RPM_STAGING="$SOLUTION_ROOT/artifacts/rpm-staging/desktop-client"
PAYLOAD_DIR="$RPM_STAGING/payload"
SPEC_FILE="$RPM_STAGING/dotnetcloud-sync-tray.spec"
RPM_OUTPUT="$OUTPUT_DIR/dotnetcloud-sync-tray-${RPM_VERSION}-1.x86_64.rpm"

echo "=============================================="
echo " DotNetCloud — Desktop Client RPM Builder"
echo " Version: $VERSION (rpm: $RPM_VERSION)"
echo "=============================================="

# Require rpmbuild
if ! command -v rpmbuild >/dev/null 2>&1; then
    echo "Error: rpmbuild is required. Fedora/RHEL: sudo dnf install rpm-build" >&2
    echo "       Ubuntu (CI): sudo apt-get install -y rpm" >&2
    exit 1
fi

mkdir -p "$OUTPUT_DIR"
rm -rf "$RPM_STAGING"

# ---- Step 1: Publish self-contained binary ----
echo ""
echo "[1/4] Publishing SyncTray for linux-x64..."
PUBLISH_DIR="$RPM_STAGING/publish"
dotnet publish "$SYNCTRAY_PROJECT" \
    --configuration "$CONFIGURATION" \
    --runtime linux-x64 \
    --self-contained true \
    --output "$PUBLISH_DIR"

# ---- Step 2: Build a payload tree (what %install copies into the RPM) ----
echo "[2/4] Creating RPM payload..."
OPT_DIR="$PAYLOAD_DIR/opt/dotnetcloud-desktop-client/SyncTray"
BIN_DIR="$PAYLOAD_DIR/usr/bin"
APPLICATIONS_DIR="$PAYLOAD_DIR/usr/share/applications"
ICONS_DIR="$PAYLOAD_DIR/usr/share/icons/hicolor/scalable/apps"

mkdir -p "$OPT_DIR" "$BIN_DIR" "$APPLICATIONS_DIR" "$ICONS_DIR"

# Binaries → /opt/dotnetcloud-desktop-client/SyncTray (same layout as .deb / tar.gz)
cp -a "$PUBLISH_DIR/." "$OPT_DIR/"
chmod 0755 "$OPT_DIR/dotnetcloud-sync-tray"

# Launcher wrapper in /usr/bin
cat > "$BIN_DIR/dotnetcloud-sync-tray" <<'EOF'
#!/usr/bin/env bash
exec /opt/dotnetcloud-desktop-client/SyncTray/dotnetcloud-sync-tray "$@"
EOF
chmod 0755 "$BIN_DIR/dotnetcloud-sync-tray"

# Icon + desktop entry
ICON_SRC="$OPT_DIR/Assets/dotnetcloud-sync-cloud.svg"
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

# ---- Step 3: Write the RPM spec ----
echo "[3/4] Writing RPM spec..."

cat > "$SPEC_FILE" <<EOF
Name:           dotnetcloud-sync-tray
Version:        $RPM_VERSION
Release:        1
Summary:        DotNetCloud Sync Client — desktop tray sync application
License:        AGPL-3.0-or-later
URL:            https://github.com/LLabmik/DotNetCloud
BuildArch:      x86_64
# Self-contained .NET bundle ships its own native libs; disable automatic
# dependency scanning so we don't emit bogus requires from the runtime.
AutoReqProv:    no

%description
DotNetCloud Sync Client (SyncTray). A desktop tray application that keeps a
local folder synchronized with a DotNetCloud server. Ships self-contained —
no .NET runtime prerequisite.

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}
cp -a $PAYLOAD_DIR/. %{buildroot}/

%files
/opt/dotnetcloud-desktop-client/SyncTray
/usr/bin/dotnetcloud-sync-tray
/usr/share/applications/dotnetcloud-sync-tray.desktop
/usr/share/icons/hicolor/scalable/apps/dotnetcloud-sync-tray.svg

%post
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database /usr/share/applications || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache /usr/share/icons/hicolor || true
fi

%postun
if [ "\$1" = "0" ]; then
    # Remove user-session .desktop files created by the app at runtime
    for userdir in /home/*; do
        userfile="\$userdir/.local/share/applications/dotnetcloud-sync-tray.desktop"
        if [ -f "\$userfile" ]; then
            rm -f "\$userfile" || true
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

# ---- Step 4: Build the RPM ----
echo "[4/4] Building RPM package..."
rm -f "$RPM_OUTPUT"

rpmbuild --define "_topdir $RPM_STAGING" \
         --define "_builddir $RPM_STAGING/BUILD" \
         --define "_sourcedir $RPM_STAGING" \
         --define "_specdir $RPM_STAGING" \
         --define "_srcrpmdir $RPM_STAGING/SRPMS" \
         --define "_rpmdir $RPM_STAGING/RPMS" \
         -bb "$SPEC_FILE" 2>&1 | tail -30

BUILT_RPM="$(find "$RPM_STAGING/RPMS" -name "*.rpm" -type f | head -n1 || true)"
if [[ -z "$BUILT_RPM" ]]; then
    echo "Error: rpmbuild did not produce a package." >&2
    exit 1
fi

cp "$BUILT_RPM" "$RPM_OUTPUT"

# Generate checksum
RPM_HASH="$(sha256sum "$RPM_OUTPUT" | awk '{print $1}')"
RPM_NAME="$(basename "$RPM_OUTPUT")"
echo "$RPM_HASH  $RPM_NAME" > "$RPM_OUTPUT.sha256"

RPM_SIZE="$(du -h "$RPM_OUTPUT" | awk '{print $1}')"

echo ""
echo "=============================================="
echo " RPM package built successfully!"
echo "=============================================="
echo "  $RPM_OUTPUT ($RPM_SIZE)"
echo "  $RPM_OUTPUT.sha256"
echo ""
echo "Install (Fedora/RHEL) with:"
echo "  sudo dnf install ./$RPM_NAME"
echo ""
echo "Install (openSUSE) with:"
echo "  sudo zypper install ./$RPM_NAME"
echo ""
echo "Uninstall with:"
echo "  sudo dnf remove dotnetcloud-sync-tray"

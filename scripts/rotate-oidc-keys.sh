#!/usr/bin/env bash
# =============================================================================
# OpenIddict Signing Key Rotation (SOC 2 CC6 / C1)
#
# The platform already rotates OpenIddict signing + encryption keys automatically
# every 90 days via OidcKeyRotationService (config: Auth:KeyRotation). This script
# provides the MANUAL / EMERGENCY rotation procedure:
#
#   1. Backs up the oidc-keys/ directory to a timestamped archive.
#   2. Generates a fresh RSA signing key (signing-key-<date>.pem) using the same
#      naming convention OidcKeyManager.GenerateRotatedKey uses.
#   3. Verifies the generated key is valid.
#   4. Prints the service-restart command needed for the new key to take effect
#      (OidcKeyRotationService loads keys at startup).
#
# Usage:
#   rotate-oidc-keys.sh [--keys-dir <path>] [--restart]
#
# Options:
#   --keys-dir <path>  oidc-keys directory (default: $DOTNETCLOUD_DATA_DIR/oidc-keys
#                      or ./oidc-keys)
#   --restart          also restart the dotnetcloud service after rotation
#   -h, --help         show this help
#
# Requirements: openssl, tar
# =============================================================================
set -euo pipefail

KEYS_DIR=""
DO_RESTART=0

usage() {
  printf '%s\n' \
    'Usage: rotate-oidc-keys.sh [--keys-dir <path>] [--restart]' \
    '' \
    'Backs up, rotates, and verifies OpenIddict signing keys (SOC 2 CC6/C1).' \
    '' \
    'Options:' \
    '  --keys-dir <path>  oidc-keys directory (default: $DOTNETCLOUD_DATA_DIR/oidc-keys or ./oidc-keys)' \
    '  --restart          also restart the dotnetcloud service after rotation' \
    '  -h, --help         show this help' \
    '' \
    'Requires: openssl, tar'
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --keys-dir) KEYS_DIR="$2"; shift 2 ;;
    --restart)  DO_RESTART=1; shift ;;
    -h|--help)  usage; exit 0 ;;
    *) echo "ERROR: unknown option '$1'" >&2; usage >&2; exit 2 ;;
  esac
done

if ! command -v openssl >/dev/null 2>&1; then
  echo "ERROR: openssl is required but was not found on PATH." >&2
  exit 1
fi

if [[ -z "$KEYS_DIR" ]]; then
  KEYS_DIR="${DOTNETCLOUD_DATA_DIR:-$(pwd)}/oidc-keys"
fi
KEYS_DIR="$(cd "$(dirname "$KEYS_DIR")" 2>/dev/null && pwd)/$(basename "$KEYS_DIR")"

echo "==> OpenIddict key rotation"
echo "    Keys directory: $KEYS_DIR"

if [[ ! -d "$KEYS_DIR" ]]; then
  echo "    Directory does not exist; creating it."
  mkdir -p "$KEYS_DIR"
fi

# --- 1. Backup --------------------------------------------------------------
STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_DIR="$(dirname "$KEYS_DIR")/oidc-keys-backup-$STAMP"
mkdir -p "$BACKUP_DIR"
cp -a "$KEYS_DIR"/. "$BACKUP_DIR"/
echo "    Backup created: $BACKUP_DIR"
echo "    Record this backup path in the audit evidence (key-rotation log)."

# --- 2. Generate a fresh signing key ---------------------------------------
DATE_STAMP="$(date +%Y-%m-%d)"
NEW_KEY="$KEYS_DIR/signing-key-$DATE_STAMP.pem"
if [[ -f "$NEW_KEY" ]]; then
  COUNTER=1
  while [[ -f "$KEYS_DIR/signing-key-$DATE_STAMP-$COUNTER.pem" ]]; do
    COUNTER=$((COUNTER + 1))
  done
  NEW_KEY="$KEYS_DIR/signing-key-$DATE_STAMP-$COUNTER.pem"
fi

echo "    Generating new signing key: $(basename "$NEW_KEY")"
openssl genrsa -out "$NEW_KEY" 2048 >/dev/null 2>&1
chmod 600 "$NEW_KEY"

# --- 3. Verify --------------------------------------------------------------
echo "    Verifying key:"
openssl rsa -in "$NEW_KEY" -check -noout

# --- 4. Restart (optional) --------------------------------------------------
echo ""
echo "==> Rotation complete."
echo "    New key: $(basename "$NEW_KEY")"
echo "    IMPORTANT: OidcKeyRotationService loads keys at startup. Restart the"
echo "    service for the new key to take effect, e.g.:"
echo "        sudo systemctl restart dotnetcloud"
echo "    Verify tokens still validate after restart (open /health and sign in)."

if [[ "$DO_RESTART" -eq 1 ]]; then
  echo "    Restarting dotnetcloud service..."
  sudo systemctl restart dotnetcloud
  echo "    Service restarted. Confirm health: curl -fsS http://localhost/health/live"
fi

echo "    Record: date=$DATE_STAMP key=$(basename "$NEW_KEY") backup=$BACKUP_DIR"
exit 0

#!/usr/bin/env bash
#
# DotNetCloud certbot deploy hook.
#
# Runs after certbot successfully issues/renews the Let's Encrypt certificate
# for cloud.dotnetcloud.net. It converts the PEM bundle into the PFX file that
# Kestrel loads, and refreshes the PEM bundle used by the Collabora Online
# integration.
#
# certbot exports these variables to deploy hooks:
#   RENEWED_LINEAGE  = /etc/letsencrypt/live/<domain>
#   RENEWED_DOMAINS  = space-delimited list of renewed domains

set -euo pipefail

CERT_DIR="/etc/dotnetcloud/certs"
PFX="${CERT_DIR}/dotnetcloud-le-cloud.dotnetcloud.net.pfx"
PEM="${CERT_DIR}/dotnetcloud-le-cloud.dotnetcloud.net.pem"

LINEAGE="${RENEWED_LINEAGE:-/etc/letsencrypt/live/cloud.dotnetcloud.net}"

# Friendly name stored inside the PFX (cosmetic only).
DOMAIN="${RENEWED_DOMAINS%% *}"
DOMAIN="${DOMAIN:-cloud.dotnetcloud.net}"

log() { echo "dotnetcloud-cert-deploy-hook: $*"; }

if [[ ! -f "${LINEAGE}/fullchain.pem" || ! -f "${LINEAGE}/privkey.pem" ]]; then
    log "ERROR: ${LINEAGE} is missing fullchain.pem/privkey.pem" >&2
    exit 1
fi

# Convert PEM -> PFX with an empty password. Kestrel loads it via
# UseHttps(path, string.Empty) because Kestrel:CertificatePassword is unset.
openssl pkcs12 -export \
    -certpbe AES-256-CBC \
    -keypbe  AES-256-CBC \
    -macalg  SHA256 \
    -inkey "${LINEAGE}/privkey.pem" \
    -in    "${LINEAGE}/fullchain.pem" \
    -out   "${PFX}" \
    -name  "DotNetCloud - ${DOMAIN}" \
    -passout pass:

# Verify the converted PFX parses before we let the service start against it.
if ! openssl pkcs12 -in "${PFX}" -noout -passin pass: >/dev/null 2>&1; then
    log "ERROR: converted PFX failed validation" >&2
    exit 1
fi

# Refresh the combined PEM bundle (private key + full chain). This matches what
# the CLI's AcmeService writes and what 'dotnetcloud cert-renew' reads when it
# syncs certificates to Collabora Online.
cat "${LINEAGE}/privkey.pem" "${LINEAGE}/fullchain.pem" > "${PEM}"

# The systemd service runs as dotnetcloud:dotnetcloud.
chown dotnetcloud:dotnetcloud "${PFX}" "${PEM}"
chmod 640 "${PFX}"
chmod 644 "${PEM}"

log "deployed renewed certificate to ${PFX}"

# --- Collabora Online (coolwsd) cert sync ---
COOL_DIR="/etc/coolwsd"
COOL_KEY="${COOL_DIR}/key.pem"
COOL_CERT="${COOL_DIR}/cert.pem"
COOL_CHAIN="${COOL_DIR}/ca-chain.cert.pem"

if [[ -d "${COOL_DIR}" ]] && systemctl list-unit-files coolwsd.service >/dev/null 2>&1; then
    # Private key.
    install -m 640 -o root -g cool "${LINEAGE}/privkey.pem" "${COOL_KEY}"

    # Split fullchain.pem: first CERTIFICATE block -> cert.pem, the rest -> ca-chain.
    : > "${COOL_CERT}"
    : > "${COOL_CHAIN}"
    awk '
      /-----BEGIN CERTIFICATE-----/ { n++ }
      n == 1 { print > "/etc/coolwsd/cert.pem" }
      n  > 1 { print > "/etc/coolwsd/ca-chain.cert.pem" }
    ' "${LINEAGE}/fullchain.pem"

    chown root:cool "${COOL_CERT}" "${COOL_CHAIN}"
    chmod 640 "${COOL_CERT}" "${COOL_CHAIN}"

    if systemctl restart coolwsd.service; then
        log "coolwsd restarted with renewed certificate"
    else
        log "WARNING: coolwsd restart failed — restart manually: sudo systemctl restart coolwsd" >&2
    fi
else
    log "coolwsd not installed — skipping Collabora cert sync"
fi

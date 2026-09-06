#!/usr/bin/env bash
# =============================================================================
# DotNetCloud container entrypoint.
#
# Generates a self-signed TLS certificate for the app's internal HTTPS loopback
# (https://localhost:5443) ONCE, into the persisted data volume, so the cert is
# stable across image rebuilds and container restarts (the host only needs to
# trust it a single time). The server must NOT regenerate it on every image
# build — that invalidated the host trust store on each rebuild.
#
# Runs as root (the image default) so it can install the cert into the container
# CA store (the app's strict loopback TLS validation rejects an untrusted
# self-signed chain), then drops privileges to the unprivileged 'app' user
# (uid/gid 1654) and execs the server.
# =============================================================================
set -euo pipefail

DATA_DIR="${DOTNETCLOUD_DATA_DIR:-/app/data}"
CERT_DIR="${DATA_DIR}/certs"
CERT="${CERT_DIR}/dotnetcloud-localhost.crt"
PFX="${CERT_DIR}/dotnetcloud-localhost.pfx"

mkdir -p "${CERT_DIR}"

if [ ! -f "${PFX}" ]; then
    echo "[entrypoint] Generating self-signed certificate in ${CERT_DIR}"
    openssl req -x509 -newkey rsa:2048 -sha256 -nodes \
        -keyout /tmp/dnc-key.pem -out "${CERT}" -days 3650 \
        -subj "/CN=dotnetcloud-internal" \
        -addext "subjectAltName=DNS:localhost,DNS:dotnetcloud,IP:127.0.0.1" \
        -addext "basicConstraints=critical,CA:TRUE" \
        -addext "keyUsage=critical,digitalSignature,keyEncipherment,keyCertSign"
    openssl pkcs12 -export -out "${PFX}" -inkey /tmp/dnc-key.pem -in "${CERT}" -passout pass:
    rm -f /tmp/dnc-key.pem
fi

# Trust the cert in the container CA store so the app's strict loopback TLS
# validation (full chain; only hostname mismatch is tolerated) accepts it.
cp "${CERT}" /usr/local/share/ca-certificates/dotnetcloud-internal.crt
update-ca-certificates >/dev/null

# Keep the persisted cert owned by the unprivileged runtime user.
chown -R app:app "${CERT_DIR}"

# Point Kestrel at the persisted cert (overrides any orchestrator-provided value).
export Kestrel__CertificatePath="${PFX}"

echo "[entrypoint] Starting DotNetCloud.Core.Server"
exec runuser -u app -- dotnet DotNetCloud.Core.Server.dll

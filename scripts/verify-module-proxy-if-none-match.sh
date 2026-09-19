#!/usr/bin/env bash
#
# Verifies that the core module API proxy forwards conditional request headers
# EXACTLY ONCE, by exercising If-None-Match end-to-end through the gateway.
#
# Background: ModuleApiProxyTransformer used to forward every request header
# twice (the base transformer already copies them, and TryAddWithoutValidation
# appends rather than replaces). Module hosts therefore saw two If-None-Match
# values and Request.Headers.IfNoneMatch.ToString() could never equal the ETag,
# so every conditional GET silently answered 200 with a full body instead of
# 304 Not Modified.
#
# This script asserts the fixed contract:
#   leg 1: authenticated GET                       -> 200 + ETag
#   leg 2: same GET with If-None-Match: <that etag> -> 304 + empty body
#
# Usage:
#   DNC_EMAIL=you@example.com ./scripts/verify-module-proxy-if-none-match.sh
#
# Optional environment overrides:
#   DNC_BASE_URL   gateway base URL        (default https://localhost:5443)
#   DNC_TARGET     module REST path        (default /api/v1/chat/alerts)
#   DNC_EMAIL      account USERNAME         (default: adminEmail from config.json)
#   DNC_CLIENT_ID  OIDC public client      (default dotnetcloud-mobile)
#
# NOTE: /auth/session/login binds the credential as [FromForm] username, so this
# value must be the account USERNAME (not the e-mail address). Accounts with MFA
# enabled cannot complete the scripted login - use a non-MFA account.
#
# The password is read interactively from the terminal. Neither the password nor
# the access token is ever echoed, logged or written to disk by this script.
#
# Requires: curl, openssl, python3. Exit codes: 0 pass, 1 assertion failed,
# 3 login failed, 4 authorize/token failed, 5 no ETag on the first leg.

set -euo pipefail

SERVER_URL="${DNC_BASE_URL:-https://localhost:5443}"
TARGET="${DNC_TARGET:-/api/v1/chat/alerts}"
CLIENT_ID="${DNC_CLIENT_ID:-dotnetcloud-mobile}"
REDIRECT_URI="${DNC_REDIRECT_URI:-net.dotnetcloud.client://oauth2redirect}"
SCOPE="openid profile email offline_access"
CONFIG_PATH="${DNC_CONFIG:-/etc/dotnetcloud/config.json}"

EMAIL="${DNC_EMAIL:-}"
if [[ -z "$EMAIL" && -r "$CONFIG_PATH" ]]; then
    EMAIL=$(python3 -c "import json;print(json.load(open('$CONFIG_PATH')).get('adminEmail',''))" 2>/dev/null || true)
fi
if [[ -z "$EMAIL" ]]; then
    echo "ERROR: set DNC_EMAIL (no adminEmail found in $CONFIG_PATH)." >&2
    exit 2
fi

COOKIE_JAR=$(mktemp)
H1=$(mktemp)
H2=$(mktemp)
B1=$(mktemp)
B2=$(mktemp)
trap 'rm -f "$COOKIE_JAR" "$H1" "$H2" "$B1" "$B2"' EXIT

echo "=== Module proxy If-None-Match verification ==="
echo "Gateway: $SERVER_URL"
echo "Target:  $TARGET"
echo "Account: $EMAIL"
echo ""

read -rsp "Password for $EMAIL: " PASSWORD
echo

# --- PKCE ---
CODE_VERIFIER=$(openssl rand -base64 32 | tr '+/' '-_' | tr -d '=')
CODE_CHALLENGE=$(printf '%s' "$CODE_VERIFIER" | openssl dgst -sha256 -binary | openssl base64 -A | tr '+/' '-_' | tr -d '=')

urlencode() { python3 -c "import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1]))" "$1"; }

# --- 1. Session login (cookie) ---
echo "[1/4] Authenticating (session login)..."
curl -sSkL -c "$COOKIE_JAR" -o /dev/null \
    -X POST "$SERVER_URL/auth/session/login" \
    --data-urlencode "username=$EMAIL" \
    --data-urlencode "password=$PASSWORD" \
    --data-urlencode "returnUrl=/" || true
unset PASSWORD

if ! grep -q "Identity.Application" "$COOKIE_JAR" 2>/dev/null; then
    echo "FAIL: no identity cookie returned - check the email/password (or MFA)." >&2
    exit 3
fi
echo "      ok"

# --- 2. Authorization code via PKCE ---
echo "[2/4] Requesting authorization code..."
AUTHORIZE_URL="${SERVER_URL}/connect/authorize?client_id=${CLIENT_ID}&redirect_uri=$(urlencode "$REDIRECT_URI")&response_type=code&scope=$(urlencode "$SCOPE")&code_challenge=${CODE_CHALLENGE}&code_challenge_method=S256&state=$(python3 -c 'import uuid;print(uuid.uuid4())')"

AUTH_HEADERS=$(curl -sSk -D - -o /dev/null -b "$COOKIE_JAR" "$AUTHORIZE_URL" --max-redirs 0 || true)
LOCATION=$(printf '%s' "$AUTH_HEADERS" | grep -i "^location:" | head -1 | tr -d '\r')
AUTH_CODE=$(printf '%s' "$LOCATION" | grep -oP 'code=[^&]+' | head -1 | cut -d= -f2 || true)

if [[ -z "$AUTH_CODE" ]]; then
    echo "FAIL: no authorization code (MFA or consent may be required for this account)." >&2
    echo "      redirect: ${LOCATION:-<none>}" >&2
    exit 4
fi
echo "      ok"

# --- 3. Exchange for a bearer token (never printed) ---
echo "[3/4] Exchanging code for a bearer token..."
TOKEN_RESPONSE=$(curl -sSk -X POST "$SERVER_URL/connect/token" \
    -d "grant_type=authorization_code" \
    -d "code=$AUTH_CODE" \
    -d "redirect_uri=$REDIRECT_URI" \
    -d "client_id=$CLIENT_ID" \
    -d "code_verifier=$CODE_VERIFIER")

TOKEN=$(printf '%s' "$TOKEN_RESPONSE" | python3 -c "import sys,json;print(json.load(sys.stdin).get('access_token',''))" 2>/dev/null || true)
if [[ -z "$TOKEN" ]]; then
    echo "FAIL: token exchange failed." >&2
    printf '%s' "$TOKEN_RESPONSE" | python3 -c "import sys,json;d=json.load(sys.stdin);print('      ',d.get('error'),d.get('error_description'))" 2>/dev/null || echo "      (unparseable response)" >&2
    exit 4
fi
echo "      ok (${#TOKEN} chars)"

# --- 4. The two legs ---
echo "[4/4] Conditional GET through the module proxy..."

status_of() { head -1 "$1" | tr -d '\r' | cut -d' ' -f2; }

curl -sSk -D "$H1" -o "$B1" -H "Authorization: Bearer $TOKEN" "$SERVER_URL$TARGET"
S1=$(status_of "$H1")
ETAG=$(grep -i "^etag:" "$H1" | head -1 | tr -d '\r' | sed -E 's/^[Ee][Tt][Aa][Gg]:[[:space:]]*//' || true)
SIZE1=$(wc -c < "$B1")

echo "      leg 1 (plain GET):                 status=$S1 bytes=$SIZE1 etag=${ETAG:-<none>}"

if [[ -z "$ETAG" ]]; then
    echo "FAIL: leg 1 returned no ETag (status $S1) - cannot test the conditional." >&2
    exit 5
fi

curl -sSk -D "$H2" -o "$B2" -H "Authorization: Bearer $TOKEN" -H "If-None-Match: $ETAG" "$SERVER_URL$TARGET"
S2=$(status_of "$H2")
SIZE2=$(wc -c < "$B2")
echo "      leg 2 (If-None-Match: same etag): status=$S2 bytes=$SIZE2"
echo ""

if [[ "$S1" == "200" && "$S2" == "304" ]]; then
    echo "PASS: conditional GET honoured - proxy forwards If-None-Match exactly once."
    echo "      (Before the fix leg 2 answered 200 with the full body.)"
    exit 0
fi

echo "FAIL: expected leg 1 = 200 and leg 2 = 304, got $S1 and $S2." >&2
if [[ "$S2" == "200" ]]; then
    echo "      A 200 on leg 2 means the ETag reached the module host duplicated or mangled." >&2
    echo "      Check ModuleApiProxyTransformer in DotNetCloud.Core.Server/Program.cs." >&2
fi
exit 1

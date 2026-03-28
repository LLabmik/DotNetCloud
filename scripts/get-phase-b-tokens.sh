#!/usr/bin/env bash
# Phase B Token Acquisition — automated OAuth2 PKCE flow via curl
# Usage: ./scripts/get-phase-b-tokens.sh [password]
set -euo pipefail

SERVER_URL="${DNC_BASE_URL:-https://mint22:5443}"
EMAIL="${DNC_EMAIL:-testdude@llabmik.net}"
CLIENT_ID="dotnetcloud-mobile"
REDIRECT_URI="net.dotnetcloud.client://oauth2redirect"
PASSWORD="${1:-}"
COOKIE_JAR=$(mktemp)
trap 'rm -f "$COOKIE_JAR"' EXIT

if [[ -z "$PASSWORD" ]]; then
    read -rsp "Password for $EMAIL: " PASSWORD
    echo
fi

echo "=== Phase B Token Acquisition ==="
echo "Server:  $SERVER_URL"
echo "User:    $EMAIL"
echo ""

# --- Step 1: Generate PKCE codes ---
echo "[1/6] Generating PKCE codes..."
CODE_VERIFIER=$(openssl rand -base64 32 | tr '+/' '-_' | tr -d '=')
CODE_CHALLENGE=$(printf '%s' "$CODE_VERIFIER" | openssl dgst -sha256 -binary | openssl base64 -A | tr '+/' '-_' | tr -d '=')
echo "  Verifier:  ${CODE_VERIFIER:0:20}..."
echo "  Challenge: ${CODE_CHALLENGE:0:20}..."
echo ""

# --- Step 2: Login via form POST to get session cookies ---
echo "[2/6] Authenticating via form login..."
LOGIN_RESPONSE=$(curl -sSk -D - -o /dev/null \
    -c "$COOKIE_JAR" \
    -X POST "$SERVER_URL/auth/session/login" \
    -d "email=$(python3 -c "import urllib.parse; print(urllib.parse.quote('$EMAIL'))")" \
    -d "password=$(python3 -c "import urllib.parse; print(urllib.parse.quote('$PASSWORD'))")" \
    -d "returnUrl=/" \
    --max-redirs 0 2>&1 || true)

# Check for cookie
if grep -q "Identity.Application" "$COOKIE_JAR" 2>/dev/null; then
    echo "  Login succeeded (cookie received)"
else
    echo "  WARNING: No Identity cookie found. Login may have failed."
    echo "  Response headers:"
    echo "$LOGIN_RESPONSE" | head -20
fi
echo ""

# --- Step 3: Request authorization code ---
echo "[3/6] Requesting authorization code..."
STATE=$(python3 -c "import uuid; print(uuid.uuid4())")
ENCODED_REDIRECT=$(python3 -c "import urllib.parse; print(urllib.parse.quote('$REDIRECT_URI'))")
AUTHORIZE_URL="${SERVER_URL}/connect/authorize?client_id=${CLIENT_ID}&redirect_uri=${ENCODED_REDIRECT}&response_type=code&scope=openid%20profile%20offline_access%20files%3Aread%20files%3Awrite&code_challenge=${CODE_CHALLENGE}&code_challenge_method=S256&state=${STATE}"

AUTH_HEADERS=$(curl -sSk -D - -o /dev/null \
    -b "$COOKIE_JAR" \
    "$AUTHORIZE_URL" \
    --max-redirs 0 2>&1 || true)

# Extract the authorization code from the Location redirect header
LOCATION=$(echo "$AUTH_HEADERS" | grep -i "^location:" | head -1 | tr -d '\r')
if [[ -z "$LOCATION" ]]; then
    echo "  ERROR: No redirect from authorize endpoint"
    echo "  Headers: $AUTH_HEADERS"
    exit 1
fi

AUTH_CODE=$(echo "$LOCATION" | grep -oP 'code=([^&]+)' | head -1 | cut -d= -f2)
if [[ -z "$AUTH_CODE" ]]; then
    echo "  ERROR: No authorization code in redirect"
    echo "  Location: $LOCATION"
    exit 1
fi
echo "  Auth code: ${AUTH_CODE:0:20}..."
echo ""

# --- Step 4: Exchange code for tokens ---
echo "[4/6] Exchanging code for bearer token..."
TOKEN_RESPONSE=$(curl -sSk \
    -X POST "$SERVER_URL/connect/token" \
    -d "grant_type=authorization_code" \
    -d "code=$AUTH_CODE" \
    -d "redirect_uri=$REDIRECT_URI" \
    -d "client_id=$CLIENT_ID" \
    -d "code_verifier=$CODE_VERIFIER" \
    -H "Content-Type: application/x-www-form-urlencoded")

ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('access_token',''))" 2>/dev/null || echo "")
if [[ -z "$ACCESS_TOKEN" ]]; then
    echo "  ERROR: Token exchange failed"
    echo "  Response: $TOKEN_RESPONSE"
    exit 1
fi
EXPIRES_IN=$(echo "$TOKEN_RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin).get('expires_in','?'))" 2>/dev/null || echo "?")
echo "  Bearer token received (${#ACCESS_TOKEN} chars, expires in ${EXPIRES_IN}s)"
echo ""

# --- Step 5: Get a file ID from the files API ---
echo "[5/6] Fetching a file ID from files API..."
FILES_RESPONSE=$(curl -sSk \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    "$SERVER_URL/api/v1/files?path=/")

FILE_ID=$(echo "$FILES_RESPONSE" | python3 -c "
import sys, json
data = json.load(sys.stdin)
items = data if isinstance(data, list) else data.get('items', data.get('data', []))
# Find first non-folder item
for item in items:
    if not item.get('isFolder', item.get('isDirectory', False)):
        print(item.get('id', ''))
        sys.exit(0)
# If no files, try first item anyway
if items:
    print(items[0].get('id', ''))
" 2>/dev/null || echo "")

if [[ -z "$FILE_ID" ]]; then
    echo "  WARNING: Could not auto-detect a file ID."
    echo "  Files response: $(echo "$FILES_RESPONSE" | head -5)"
    echo "  You may need to set DNC_FILE_ID manually."
else
    echo "  File ID: $FILE_ID"
fi
echo ""

# --- Step 6: Get WOPI token (if we have a file ID) ---
WOPI_TOKEN=""
if [[ -n "$FILE_ID" ]]; then
    echo "[6/6] Generating WOPI token..."
    WOPI_RESPONSE=$(curl -sSk \
        -X POST \
        -H "Authorization: Bearer $ACCESS_TOKEN" \
        "$SERVER_URL/api/v1/wopi/token/$FILE_ID")

    WOPI_TOKEN=$(echo "$WOPI_RESPONSE" | python3 -c "
import sys, json
data = json.load(sys.stdin)
token_data = data.get('data', data)
print(token_data.get('accessToken', token_data.get('access_token', '')))
" 2>/dev/null || echo "")

    if [[ -z "$WOPI_TOKEN" ]]; then
        echo "  WARNING: Could not get WOPI token."
        echo "  Response: $(echo "$WOPI_RESPONSE" | head -3)"
        echo "  (WOPI test TC-1.27 can be skipped if no Collabora-compatible file)"
    else
        echo "  WOPI token: ${WOPI_TOKEN:0:20}..."
    fi
else
    echo "[6/6] Skipping WOPI token (no file ID)"
fi
echo ""

# --- Output ---
echo "==========================================="
echo "  Phase B Environment Variables"
echo "==========================================="
echo ""
echo "export DNC_BASE_URL=\"$SERVER_URL\""
echo "export DNC_BEARER_TOKEN=\"$ACCESS_TOKEN\""
echo "export DNC_FILE_ID=\"$FILE_ID\""
echo "export DNC_WOPI_TOKEN=\"$WOPI_TOKEN\""
echo "export DNC_SINCE=\"2026-03-25T00:00:00Z\""
echo ""
echo "==========================================="

# Save to a sourceable file
cat > /tmp/phase-b-env.sh << EOF
export DNC_BASE_URL="$SERVER_URL"
export DNC_BEARER_TOKEN="$ACCESS_TOKEN"
export DNC_FILE_ID="$FILE_ID"
export DNC_WOPI_TOKEN="$WOPI_TOKEN"
export DNC_SINCE="2026-03-25T00:00:00Z"
EOF
echo "Saved to /tmp/phase-b-env.sh — run: source /tmp/phase-b-env.sh"

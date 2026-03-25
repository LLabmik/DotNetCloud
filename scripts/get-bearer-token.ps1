# Get bearer token for API testing
# This script performs OAuth2 authorization code + PKCE flow to obtain a bearer token

param(
    [string]$ServerUrl = "https://mint22:5443",
    [string]$Email = "testdude@llabmik.net",
    [string]$Password = "",
    [string]$ClientId = "dotnetcloud-mobile",
    [string]$RedirectUri = "net.dotnetcloud.client://oauth2redirect"
)

function Generate-CodeVerifier {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $verifier = [Convert]::ToBase64String($bytes) -replace '\+', '-' -replace '/', '_' -replace '=', ''
    return $verifier
}

function Generate-CodeChallenge {
    param([string]$verifier)
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($verifier)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $hash = $sha256.ComputeHash($bytes)
    $challenge = [Convert]::ToBase64String($hash) -replace '\+', '-' -replace '/', '_' -replace '=', ''
    return $challenge
}

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "OAuth2 PKCE Token Acquisition" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Generate PKCE codes
Write-Host "[1/5] Generating PKCE codes..." -ForegroundColor Yellow
$codeVerifier = Generate-CodeVerifier
$codeChallenge = Generate-CodeChallenge -verifier $codeVerifier
Write-Host "  Code Verifier: $($codeVerifier.Substring(0, 20))..." -ForegroundColor Gray
Write-Host "  Code Challenge: $($codeChallenge.Substring(0, 20))..." -ForegroundColor Gray
Write-Host ""

# Step 2: Authenticate with form login to get cookies
Write-Host "[2/5] Authenticating user via web form..." -ForegroundColor Yellow
$loginUrl = "$ServerUrl/auth/session/login"
$loginBody = @{
    email = $Email
    password = $Password
    returnUrl = "/"
}

try {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginResponse = Invoke-WebRequest -Uri $loginUrl -Method Post -Body $loginBody -SessionVariable session -MaximumRedirection 0 -SkipCertificateCheck -ErrorAction SilentlyContinue
    Write-Host "  Login response: $($loginResponse.StatusCode)" -ForegroundColor Gray
    Write-Host "  Cookies: $($session.Cookies.GetCookies($ServerUrl).Count) cookies received" -ForegroundColor Gray
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        Write-Host "  Login succeeded (redirect to $($_.Exception.Response.Headers['Location']))" -ForegroundColor Green
    } else {
        Write-Host "  Login failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# Step 3: Hit authorize endpoint with cookies to get authorization code
Write-Host "[3/5] Requesting authorization code..." -ForegroundColor Yellow
$state = [Guid]::NewGuid().ToString()
$authorizeUrl = "$ServerUrl/connect/authorize?" + 
    "client_id=$ClientId&" +
    "redirect_uri=$([Uri]::EscapeDataString($RedirectUri))&" +
    "response_type=code&" +
    "scope=openid%20profile%20email%20offline_access%20files:read%20files:write&" +
    "code_challenge=$codeChallenge&" +
    "code_challenge_method=S256&" +
    "state=$state"

try {
    $authResponse = Invoke-WebRequest -Uri $authorizeUrl -Method Get -WebSession $session -MaximumRedirection 0 -SkipCertificateCheck -ErrorAction SilentlyContinue
} catch {
    if ($_.Exception.Response.StatusCode -eq 302) {
        $location = $_.Exception.Response.Headers['Location']
        Write-Host "  Redirect to: $location" -ForegroundColor Gray
        
        # Extract authorization code from redirect URI
        if ($location -match "code=([^&]+)") {
            $authCode = $matches[1]
            Write-Host "  Authorization code: $($authCode.Substring(0, 20))..." -ForegroundColor Green
        } else {
            Write-Host "  ERROR: No authorization code in redirect" -ForegroundColor Red
            Write-Host "  Full redirect: $location" -ForegroundColor Gray
            exit 1
        }
    } else {
        Write-Host "  Authorization failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# Step 4: Exchange authorization code for tokens
Write-Host "[4/5] Exchanging code for bearer token..." -ForegroundColor Yellow
$tokenUrl = "$ServerUrl/connect/token"
$tokenBody = @{
    grant_type = "authorization_code"
    code = $authCode
    redirect_uri = $RedirectUri
    client_id = $ClientId
    code_verifier = $codeVerifier
}

try {
    $tokenResponse = Invoke-RestMethod -Uri $tokenUrl -Method Post -Body $tokenBody -ContentType "application/x-www-form-urlencoded" -SkipCertificateCheck
    Write-Host "  Token received successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Step 5: Display token info
    Write-Host "[5/5] Token Information:" -ForegroundColor Yellow
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "Access Token: $($tokenResponse.access_token.Substring(0, 50))..." -ForegroundColor Green
    Write-Host "Token Type: $($tokenResponse.token_type)" -ForegroundColor Gray
    Write-Host "Expires In: $($tokenResponse.expires_in) seconds" -ForegroundColor Gray
    if ($tokenResponse.refresh_token) {
        Write-Host "Refresh Token: $($tokenResponse.refresh_token.Substring(0, 50))..." -ForegroundColor Gray
    }
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Full bearer token saved to: bearer-token.txt" -ForegroundColor Yellow
    
    # Save tokens to file
    $tokenResponse.access_token | Out-File -FilePath "bearer-token.txt" -Encoding UTF8 -NoNewline
    $tokenResponse | ConvertTo-Json | Out-File -FilePath "token-response.json" -Encoding UTF8
    
    return $tokenResponse
    
} catch {
    Write-Host "  Token exchange failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "  Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}

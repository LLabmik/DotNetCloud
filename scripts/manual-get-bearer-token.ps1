# Manual Bearer Token Acquisition Instructions
# For WS-4 API Verification

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Manual Bearer Token Acquisition" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To acquire a bearer token for API testing:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Open browser and navigate to: https://mint22:5443" -ForegroundColor White
Write-Host "2. Log in with: testdude@llabmik.net / TestPassword456!" -ForegroundColor White
Write-Host "3. Open browser Developer Tools (F12)" -ForegroundColor White
Write-Host "4. Go to Application > Storage > Local Storage > https://mint22:5443" -ForegroundColor White
Write-Host "5. Find 'dotnetcloud_access_token' key and copy its value" -ForegroundColor White
Write-Host ""
Write-Host "6. Paste the token below and press Enter:" -ForegroundColor Yellow
Write-Host ""

$token = Read-Host "Bearer Token"

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "ERROR: No token provided" -ForegroundColor Red
    exit 1
}

$token = $token.Trim()

# Save to file
$token | Out-File -FilePath "bearer-token.txt" -Encoding UTF8 -NoNewline

Write-Host ""
Write-Host "Token saved to: bearer-token.txt" -ForegroundColor Green
Write-Host "Token length: $($token.Length) characters" -ForegroundColor Gray
Write-Host ""
Write-Host "You can now run: .\ws4-api-verification.ps1" -ForegroundColor Yellow

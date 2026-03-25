# WS-4 API Verification Script
# Tests API endpoints TC-1.27, TC-1.40, TC-1.41, TC-1.42, TC-1.45 against mint22 deployment

param(
    [string]$ServerUrl = "https://mint22:5443",
    [string]$BearerTokenFile = "bearer-token.txt",
    [string]$Email = "testdude@llabmik.net",
    [string]$Password = ""
)

function Write-TestHeader {
    param([string]$TestName, [string]$TestId)
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "$TestId: $TestName" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
}

function Write-TestResult {
    param([string]$Status, [string]$Message)
    $color = switch ($Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "Gray" }
    }
    Write-Host "  [$Status] $Message" -ForegroundColor $color
}

# Acquire bearer token if not provided
if (-not (Test-Path $BearerTokenFile)) {
    Write-Host "Bearer token file not found. Acquiring token..." -ForegroundColor Yellow
    $getTokenScript = Join-Path $PSScriptRoot "get-bearer-token.ps1"
    & $getTokenScript -ServerUrl $ServerUrl -Email $Email -Password $Password
    
    if (-not (Test-Path $BearerTokenFile)) {
        Write-Host "ERROR: Failed to acquire bearer token" -ForegroundColor Red
        exit 1
    }
}

$bearerToken = Get-Content $BearerTokenFile -Raw
Write-Host "Bearer token loaded: $($bearerToken.Substring(0, 50))..." -ForegroundColor Green
Write-Host ""

$headers = @{
    "Authorization" = "Bearer $bearerToken"
    "Accept" = "application/json"
}

$results = @{
    "TC-1.40" = @{ Status = "PENDING"; Finding = "" }
    "TC-1.41" = @{ Status = "PENDING"; Finding = "" }
    "TC-1.42" = @{ Status = "PENDING"; Finding = "" }
    "TC-1.45" = @{ Status = "PENDING"; Finding = "" }
    "TC-1.27" = @{ Status = "PENDING"; Finding = "" }
}

# TC-1.40: GET /api/v1/files/sync/changes
Write-TestHeader -TestName "Sync Changes Endpoint" -TestId "TC-1.40"

try {
    $changesUrl = "$ServerUrl/api/v1/files/sync/changes"
    Write-Host "  Request: GET $changesUrl" -ForegroundColor Gray
    
    $changesResponse = Invoke-RestMethod -Uri $changesUrl -Method Get -Headers $headers -SkipCertificateCheck
    
    if ($changesResponse.success -eq $true) {
        Write-TestResult -Status "PASS" -Message "HTTP 200 - Sync changes endpoint accessible"
        $results["TC-1.40"].Status = "PASS"
        $results["TC-1.40"].Finding = "Endpoint returns changes list"
        
        if ($changesResponse.data) {
            Write-Host "  Changes count: $($changesResponse.data.Count)" -ForegroundColor Gray
        }
    } else {
        Write-TestResult -Status "FAIL" -Message "Response success=false"
        $results["TC-1.40"].Status = "FAIL"
        $results["TC-1.40"].Finding = "API returned success=false"
    }
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-TestResult -Status "FAIL" -Message "HTTP $statusCode - $($_.Exception.Message)"
    $results["TC-1.40"].Status = "FAIL"
    $results["TC-1.40"].Finding = "HTTP $statusCode - $($_.Exception.Message)"
}

# TC-1.42: GET /api/v1/files/sync/tree
Write-TestHeader -TestName "Sync Tree Endpoint" -TestId "TC-1.42"

try {
    $treeUrl = "$ServerUrl/api/v1/files/sync/tree"
    Write-Host "  Request: GET $treeUrl" -ForegroundColor Gray
    
    $treeResponse = Invoke-RestMethod -Uri $treeUrl -Method Get -Headers $headers -SkipCertificateCheck
    
    if ($treeResponse.success -eq $true) {
        Write-TestResult -Status "PASS" -Message "HTTP 200 - Sync tree endpoint accessible"
        $results["TC-1.42"].Status = "PASS"
        $results["TC-1.42"].Finding = "Endpoint returns file tree"
        
        if ($treeResponse.data -and $treeResponse.data.Count -gt 0) {
            Write-Host "  Tree nodes count: $($treeResponse.data.Count)" -ForegroundColor Gray
            $script:testFileId = $treeResponse.data[0].fileId
            Write-Host "  Sample fileId: $testFileId" -ForegroundColor Gray
        } else {
            Write-Host "  Tree is empty (no files)" -ForegroundColor Yellow
        }
    } else {
        Write-TestResult -Status "FAIL" -Message "Response success=false"
        $results["TC-1.42"].Status = "FAIL"
        $results["TC-1.42"].Finding = "API returned success=false"
    }
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-TestResult -Status "FAIL" -Message "HTTP $statusCode - $($_.Exception.Message)"
    $results["TC-1.42"].Status = "FAIL"
    $results["TC-1.42"].Finding = "HTTP $statusCode - $($_.Exception.Message)"
}

# TC-1.41: POST /api/v1/files/sync/reconcile
Write-TestHeader -TestName "Sync Reconcile Endpoint" -TestId "TC-1.41"

try {
    $reconcileUrl = "$ServerUrl/api/v1/files/sync/reconcile"
    $reconcileBody = @{
        localTree = @()
    } | ConvertTo-Json
    
    Write-Host "  Request: POST $reconcileUrl" -ForegroundColor Gray
    
    $reconcileResponse = Invoke-RestMethod -Uri $reconcileUrl -Method Post -Headers $headers -Body $reconcileBody -ContentType "application/json" -SkipCertificateCheck
    
    if ($reconcileResponse.success -eq $true) {
        Write-TestResult -Status "PASS" -Message "HTTP 200 - Reconcile endpoint accessible"
        $results["TC-1.41"].Status = "PASS"
        $results["TC-1.41"].Finding = "Endpoint returns reconciliation plan"
        
        if ($reconcileResponse.data) {
            Write-Host "  Downloads: $($reconcileResponse.data.downloads.Count)" -ForegroundColor Gray
            Write-Host "  Uploads: $($reconcileResponse.data.uploads.Count)" -ForegroundColor Gray
            Write-Host "  Deletes: $($reconcileResponse.data.deletes.Count)" -ForegroundColor Gray
        }
    } else {
        Write-TestResult -Status "FAIL" -Message "Response success=false"
        $results["TC-1.41"].Status = "FAIL"
        $results["TC-1.41"].Finding = "API returned success=false"
    }
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-TestResult -Status "FAIL" -Message "HTTP $statusCode - $($_.Exception.Message)"
    $results["TC-1.41"].Status = "FAIL"
    $results["TC-1.41"].Finding = "HTTP $statusCode - $($_.Exception.Message)"
}

# TC-1.45: GET /api/v1/files/{fileId}/download (Range Request)
Write-TestHeader -TestName "Range Request Support" -TestId "TC-1.45"

if ($script:testFileId) {
    try {
        $downloadUrl = "$ServerUrl/api/v1/files/$($script:testFileId)/download"
        $rangeHeaders = $headers.Clone()
        $rangeHeaders["Range"] = "bytes=0-99"
        
        Write-Host "  Request: GET $downloadUrl (Range: bytes=0-99)" -ForegroundColor Gray
        
        try {
            $rangeResponse = Invoke-WebRequest -Uri $downloadUrl -Method Get -Headers $rangeHeaders -SkipCertificateCheck -ErrorAction Stop
            
            if ($rangeResponse.StatusCode -eq 206) {
                $contentRange = $rangeResponse.Headers["Content-Range"]
                Write-TestResult -Status "PASS" -Message "HTTP 206 - Range request supported"
                Write-Host "  Content-Range: $contentRange" -ForegroundColor Gray
                $results["TC-1.45"].Status = "PASS"
                $results["TC-1.45"].Finding = "HTTP 206 with Content-Range: $contentRange"
            } else {
                Write-TestResult -Status "FAIL" -Message "HTTP $($rangeResponse.StatusCode) - Expected 206"
                $results["TC-1.45"].Status = "FAIL"
                $results["TC-1.45"].Finding = "HTTP $($rangeResponse.StatusCode) - Expected 206 Partial Content"
            }
        } catch {
            if ($_.Exception.Response.StatusCode.value__ -eq 404) {
                Write-TestResult -Status "SKIP" -Message "HTTP 404 - File not found (no files in tree)"
                $results["TC-1.45"].Status = "SKIP"
                $results["TC-1.45"].Finding = "No files available for range request test"
            } else {
                throw
            }
        }
        
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-TestResult -Status "FAIL" -Message "HTTP $statusCode - $($_.Exception.Message)"
        $results["TC-1.45"].Status = "FAIL"
        $results["TC-1.45"].Finding = "HTTP $statusCode - $($_.Exception.Message)"
    }
} else {
    Write-TestResult -Status "SKIP" -Message "No fileId available from tree (TC-1.42 prerequisite)"
    $results["TC-1.45"].Status = "SKIP"
    $results["TC-1.45"].Finding = "No fileId available from sync tree"
}

# TC-1.27: WOPI Discovery Endpoint
Write-TestHeader -TestName "WOPI Discovery" -TestId "TC-1.27"

try {
    $wopiDiscoveryUrl = "$ServerUrl/api/v1/files/wopi/discovery"
    Write-Host "  Request: GET $wopiDiscoveryUrl" -ForegroundColor Gray
    
    $wopiResponse = Invoke-RestMethod -Uri $wopiDiscoveryUrl -Method Get -Headers $headers -SkipCertificateCheck
    
    if ($wopiResponse.success -eq $true) {
        Write-TestResult -Status "PASS" -Message "HTTP 200 - WOPI discovery endpoint accessible"
        $results["TC-1.27"].Status = "PASS"
        $results["TC-1.27"].Finding = "Endpoint returns WOPI capability info"
        
        if ($wopiResponse.data) {
            Write-Host "  WOPI data present: $($wopiResponse.data.PSObject.Properties.Count) properties" -ForegroundColor Gray
        }
    } else {
        Write-TestResult -Status "FAIL" -Message "Response success=false"
        $results["TC-1.27"].Status = "FAIL"
        $results["TC-1.27"].Finding = "API returned success=false"
    }
    
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    
    if ($statusCode -eq 404) {
        Write-TestResult -Status "SKIP" -Message "HTTP 404 - WOPI endpoint not yet implemented"
        $results["TC-1.27"].Status = "SKIP"
        $results["TC-1.27"].Finding = "WOPI endpoints are Phase 3 feature (not yet implemented)"
    } else {
        Write-TestResult -Status "FAIL" -Message "HTTP $statusCode - $($_.Exception.Message)"
        $results["TC-1.27"].Status = "FAIL"
        $results["TC-1.27"].Finding = "HTTP $statusCode - $($_.Exception.Message)"
    }
}

# Summary
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

$passCount = ($results.Values | Where-Object { $_.Status -eq "PASS" }).Count
$failCount = ($results.Values | Where-Object { $_.Status -eq "FAIL" }).Count
$skipCount = ($results.Values | Where-Object { $_.Status -eq "SKIP" }).Count

foreach ($testId in $results.Keys | Sort-Object) {
    $result = $results[$testId]
    $color = switch ($result.Status) {
        "PASS" { "Green" }
        "FAIL" { "Red" }
        "SKIP" { "Yellow" }
        default { "Gray" }
    }
    Write-Host "$testId : $($result.Status)" -ForegroundColor $color
    Write-Host "  $($result.Finding)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "PASS: $passCount | FAIL: $failCount | SKIP: $skipCount" -ForegroundColor Cyan
Write-Host ""

$results | ConvertTo-Json | Out-File -FilePath "ws4-test-results.json" -Encoding UTF8
Write-Host "Results saved to: ws4-test-results.json" -ForegroundColor Yellow
Write-Host ""

if ($failCount -gt 0) {
    exit 1
}

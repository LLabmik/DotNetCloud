<#
.SYNOPSIS
    SOC 2 Type II Compliance Scanner (PowerShell / Windows-native).

.DESCRIPTION
    Crawls a DotNetCloud repository and reports findings mapped to the SOC 2 Trust
    Services Criteria (CC*, A*, PI*, C*, P*), plus module/project coverage. This is the
    Windows equivalent of scripts/soc2-compliance-scan.sh and requires NO ripgrep — it
    uses PowerShell's Select-String, so it works on Windows hosts where `rg` is absent.

.PARAMETER Mode
    Output mode: markdown (default), txt, or ci (writes JSON + exit code).

.PARAMETER Target
    Repository root to scan (default: current directory).

.EXAMPLE
    pwsh soc2-compliance-scan.ps1 -Mode markdown -Target C:\Repos\DotNetCloud
    pwsh soc2-compliance-scan.ps1 -Mode ci -Target C:\Repos\DotNetCloud
#>
[CmdletBinding()]
param(
    [ValidateSet('markdown', 'txt', 'ci')]
    [string]$Mode = 'markdown',
    [string]$Target = '.'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Target)) {
    Write-Error "Cannot access target directory '$Target'."
    exit 1
}
$Target = (Resolve-Path -LiteralPath $Target).Path

$ReportPrefix = 'soc2-compliance-report'
$Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
# Report output directory. SOC2_REPORT_DIR (set by the server) overrides the scan
# target, which can be read-only for the service account when auditing a repo it
# does not own. The scan itself always reads from $Target.
$ReportDir = if ($env:SOC2_REPORT_DIR) { $env:SOC2_REPORT_DIR } else { $Target }
if (-not (Test-Path -LiteralPath $ReportDir)) {
    New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null
}
switch ($Mode) {
    'markdown' { $Report = Join-Path $ReportDir "$ReportPrefix-$Stamp.md" }
    'txt' { $Report = Join-Path $ReportDir "$ReportPrefix-$Stamp.txt" }
    'ci' { $Report = Join-Path $ReportDir "$ReportPrefix.json" }
}

# ---------------------------------------------------------------------------
# Exclusions (docs/SOC2_TYPE_II_COMPLIANCE_PLAN.md §3.3)
# ---------------------------------------------------------------------------
$ExcludedSegments = @('tests', 'bin', 'obj', 'node_modules', '.git', 'packages',
    '.vs', 'dist', 'build', 'artifacts', '.terraform', 'vendor', '.idea', 'wwwroot')
$ExcludedFilePatterns = @('*.deps.json', '*.min.js', '*.min.css', '*.map',
    '*Designer.cs', 'soc2-compliance-report-*', 'soc2-compliance-report.json',
    'sixlabors.lic', 'soc2-compliance-scan.sh', 'soc2-compliance-scan.ps1')

function Get-ScanFiles {
    param([string]$Root)
    Get-ChildItem -LiteralPath $Root -Recurse -File -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $rel = $_.FullName.Substring($Root.Length).TrimStart([char[]]@('\', '/'))
        $parts = $rel -split '[\\/]'
        # Root deploy-artifact modules/ directory only (NOT src/Modules).
        if ($parts.Count -gt 1 -and $parts[0] -eq 'modules') { return $false }
        for ($i = 0; $i -lt $parts.Length - 1; $i++) {
            if ($ExcludedSegments -contains $parts[$i]) { return $false }
        }
        foreach ($pat in $ExcludedFilePatterns) {
            if ($_.Name -like $pat) { return $false }
        }
        return $true
    }
}

$CodeExtensions = @('.cs', '.ts', '.tsx', '.js', '.jsx', '.mjs', '.cjs', '.razor',
    '.cshtml', '.html', '.ps1', '.psm1', '.sh', '.bash', '.py', '.go', '.java',
    '.kt', '.kts', '.swift', '.dart', '.cpp', '.c', '.h', '.hpp', '.cc', '.rs',
    '.rb', '.php', '.vue', '.scss', '.css')

function Get-CodeFiles { param($Files) $Files | Where-Object { $CodeExtensions -contains $_.Extension } }
function Get-ConfigFiles {
    param($Files)
    $Files | Where-Object {
        $_.Name -like 'appsettings*.json' -or $_.Name -like '*.env*' -or
        $_.Name -like 'Dockerfile*' -or $_.Name -like 'docker-compose*.yml' -or
        $_.Name -like 'docker-compose*.yaml' -or
        $_.FullName -match '[\\/]\.github[\\/]workflows[\\/]'
    }
}
function Get-PiiFiles {
    param($Files)
    $Files | Where-Object {
        $_.FullName -match '[\\/](Entities|DTOs|Models)[\\/]' -and
        ($_.Extension -in '.cs', '.razor', '.ts')
    }
}

# ---------------------------------------------------------------------------
# Results accumulator
# ---------------------------------------------------------------------------
$Checks = New-Object System.Collections.Generic.List[object]   # tag,label,lines

function Add-Check {
    param([string]$Tag, [string]$Label, [string[]]$Lines)
    $Checks.Add([pscustomobject]@{ Tag = $Tag; Label = $Label; Lines = $Lines })
}

# Run Select-String over files; returns "path:line:content" strings.
function Invoke-RgLike {
    param([object[]]$Files, [string]$Pattern, [bool]$CaseSensitive = $false)
    if (-not $Files -or $Files.Count -eq 0) { return @() }
    $paths = @($Files | ForEach-Object { $_.FullName })
    $out = @()
    $matches = Select-String -Path $paths -Pattern $Pattern -CaseSensitive:$CaseSensitive -ErrorAction SilentlyContinue
    foreach ($m in $matches) {
        $out += "$($m.Path):$($m.LineNumber):$($m.Line.Trim())"
    }
    return $out
}

$AllFiles = @(Get-ScanFiles -Root $Target)
$CodeFiles = @(Get-CodeFiles -Files $AllFiles)
$ConfigFiles = @(Get-ConfigFiles -Files $AllFiles)
$PiiFiles = @(Get-PiiFiles -Files $AllFiles)

$SecretPattern = 'Password|ApiKey|ApiSecret|ClientSecret|ClientId|ConnectionString|Bearer [A-Za-z0-9]'
$WeakCryptoPattern = '\b(MD5|SHA1|DES|RC4|AesManaged|RijndaelManaged|TripleDES)\b'
$TlsBypassPattern = 'DangerousAcceptAnyServerCertificateValidator|ServerCertificateCustomValidationCallback|AllowInsecureTls'
$InsecureGrpcPattern = 'ChannelCredentials\.Insecure'
$RawSqlPattern = 'FromSqlRaw|ExecuteSqlRaw|SqlQueryRaw'
$OpenRedirectPattern = 'Redirect\(|RedirectToAction|RedirectToPage|Url\.IsLocalUrl'
$SecurityWords = '\b(security|auth[a-z]*|encrypt[a-z]*)\b'
$TodoPattern = "(TODO|FIXME)[^\r\n]*$SecurityWords|$SecurityWords[^\r\n]*(TODO|FIXME)"
$PiiPattern = '\b(Email|Phone|Address|BirthDate|Ssn|SocialSecurity|Passport|Latitude|Longitude|Gps|Geolocation)\b'
$UploadPattern = 'IFormFile|FromForm|RequestSizeLimit'
$RetentionPattern = 'RetentionDays|Purge|RetainedFileCountLimit|AuditFilePath'
$Ipv4Pattern = '(?<![0-9.])(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])(\.(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])){3}(?![0-9.])'

# 1. Secrets in config-like files (CC6/CC7)
Add-Check 'CC6/CC7' '1. Secrets in config files (appsettings*, *.env*, Dockerfile, docker-compose, workflows)' `
(Invoke-RgLike -Files $ConfigFiles -Pattern $SecretPattern)

# 1b. .env files present (CC6/CC7)
$envFiles = @($AllFiles | Where-Object { $_.Name -like '*.env*' })
Add-Check 'CC6/CC7' '1b. .env files found' @($envFiles | ForEach-Object { $_.FullName })

# 2. Private keys (C1)
Add-Check 'C1' '2. Private key material (-----BEGIN ... PRIVATE KEY-----)' `
(Invoke-RgLike -Files $AllFiles -Pattern '-----BEGIN (RSA|EC|OPENSSH )?PRIVATE KEY-----')

# 3. Weak crypto (C1)
Add-Check 'C1' '3. Weak/hostile crypto algorithms (MD5, SHA1, DES, RC4, AesManaged, RijndaelManaged, TripleDES)' `
(Invoke-RgLike -Files $CodeFiles -Pattern $WeakCryptoPattern)

# 4. TLS bypass (C1)
Add-Check 'C1' '4. TLS validation bypass (DangerousAcceptAnyServerCertificateValidator / AllowInsecureTls / callback)' `
(Invoke-RgLike -Files $CodeFiles -Pattern $TlsBypassPattern)

# 5. Insecure gRPC channel (C1)
Add-Check 'C1' '5. Insecure gRPC channel (ChannelCredentials.Insecure)' `
(Invoke-RgLike -Files $CodeFiles -Pattern $InsecureGrpcPattern -CaseSensitive $true)

# 6. Raw SQL (PI1)
Add-Check 'PI1' '6. Raw SQL with potential user input (FromSqlRaw / ExecuteSqlRaw / SqlQueryRaw)' `
(Invoke-RgLike -Files $CodeFiles -Pattern $RawSqlPattern -CaseSensitive $true)

# 7. AllowedHosts present in appsettings*.json (CC6) — reports files MISSING it
$missingHosts = @()
foreach ($f in @($AllFiles | Where-Object { $_.Name -like 'appsettings*.json' })) {
    if (-not (Select-String -LiteralPath $f.FullName -Pattern 'AllowedHosts' -Quiet -ErrorAction SilentlyContinue)) {
        $missingHosts += "$($f.FullName):1:(file lacks AllowedHosts)"
    }
}
Add-Check 'CC6' '7. appsettings*.json missing AllowedHosts' $missingHosts

# 8. Open redirect (CC6) — manual review list
Add-Check 'CC6' '8. Open-redirect patterns (Redirect( / RedirectToAction / RedirectToPage / Url.IsLocalUrl) - REVIEW' `
(Invoke-RgLike -Files $CodeFiles -Pattern $OpenRedirectPattern -CaseSensitive $true)

# 9. TODO/FIXME mentioning security/auth/encryption (CC7)
Add-Check 'CC7' '9. TODO/FIXME comments mentioning security/auth/encryption' `
(Invoke-RgLike -Files $CodeFiles -Pattern $TodoPattern)

# 10. PII field names in Entities/DTOs/Models (P6)
Add-Check 'P6' '10. PII field names in Entities/DTOs/Models' `
(Invoke-RgLike -Files $PiiFiles -Pattern $PiiPattern)

# 11. Upload entry points (PI1)
Add-Check 'PI1' '11. Upload entry points (IFormFile / FromForm / RequestSizeLimit) - verify validation wired' `
(Invoke-RgLike -Files $CodeFiles -Pattern $UploadPattern -CaseSensitive $true)

# 12. Retention / disposal markers (C2/P6)
Add-Check 'C2/P6' '12. Retention/disposal markers (RetentionDays / Purge / RetainedFileCountLimit / AuditFilePath)' `
(Invoke-RgLike -Files $CodeFiles -Pattern $RetentionPattern)

# 13. Hardcoded IPv4 (CC7) — version/assembly declarations filtered
$ipLines = @()
foreach ($m in (Select-String -Path @($AllFiles | ForEach-Object { $_.FullName }) -Pattern $Ipv4Pattern -ErrorAction SilentlyContinue)) {
    if ($m.Line -match 'Version=|AssemblyVersion|FileVersion|InformationalVersion|PackageVersion|PublicKeyToken|Culture=') { continue }
    $ipLines += "$($m.Path):$($m.LineNumber):$($m.Line.Trim())"
}
Add-Check 'CC7' '13. Hardcoded IPv4 addresses' $ipLines

# ---------------------------------------------------------------------------
# Module & project coverage
# ---------------------------------------------------------------------------
$ModuleIds = @('files', 'chat', 'contacts', 'calendar', 'notes', 'about',
    'ai', 'bookmarks', 'email', 'example', 'music', 'photos', 'tracks', 'video')
$ClientDirs = @(
    'src\Clients\DotNetCloud.Client.Core',
    'src\Clients\DotNetCloud.Client.SyncTray',
    'src\Clients\DotNetCloud.Client.Android',
    'src\Clients\DotNetCloud.Client.Updater',
    'src\Clients\DotNetCloud.Client.BrowserExtension',
    'src\UI\DotNetCloud.UI.Web',
    'src\UI\DotNetCloud.UI.Web.Client',
    'src\UI\DotNetCloud.UI.Shared',
    'src\UI\DotNetCloud.UI.Android',
    'src\CLI\DotNetCloud.CLI',
    'src\Core\DotNetCloud.Core',
    'src\Core\DotNetCloud.Core.Data',
    'src\Core\DotNetCloud.Core.ServiceDefaults',
    'src\Core\DotNetCloud.Core.Grpc',
    'src\Core\DotNetCloud.Core.Auth',
    'src\Core\DotNetCloud.Core.Server'
)

function Get-CoverageRow {
    param([string]$Dir, [string]$Name)
    if (-not (Test-Path -LiteralPath $Dir)) {
        return [pscustomobject]@{ Name = $Name; Files = 0; Status = 'MISSING' }
    }
    $count = @(Get-ChildItem -LiteralPath $Dir -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.cs', '.csproj', '.ts', '.js' }).Count
    $status = if ($count -gt 0) { 'ok' } else { 'MISSING' }
    return [pscustomobject]@{ Name = $Name; Files = $count; Status = $status }
}

$CoverageRows = New-Object System.Collections.Generic.List[object]
$MissingCoverage = 0
$TotalScanned = 0
$modulesRoot = Join-Path $Target 'src\Modules'
foreach ($id in $ModuleIds) {
    $dir = Get-ChildItem -LiteralPath $modulesRoot -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ieq $id } | Select-Object -First 1
    if (-not $dir) {
        $CoverageRows.Add([pscustomobject]@{ Name = $id; Files = 0; Status = 'MISSING' })
        $MissingCoverage++
        continue
    }
    $count = @(Get-ChildItem -LiteralPath $dir.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in '.cs', '.csproj' }).Count
    $CoverageRows.Add([pscustomobject]@{ Name = $id; Files = $count; Status = if ($count -gt 0) { 'ok' } else { 'MISSING' } })
    $TotalScanned += $count
    if ($count -eq 0) { $MissingCoverage++ }
}
foreach ($d in $ClientDirs) {
    $name = Split-Path $d -Leaf
    $row = Get-CoverageRow -Dir (Join-Path $Target $d) -Name $name
    $CoverageRows.Add($row)
    $TotalScanned += $row.Files
    if ($row.Status -eq 'MISSING') { $MissingCoverage++ }
}

$TotalFindings = 0
foreach ($c in $Checks) { $TotalFindings += $c.Lines.Count }

# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------
if ($Mode -eq 'ci') {
    $json = [ordered]@{
        generated        = (Get-Date -Format o)
        target           = $Target
        total_findings   = $TotalFindings
        missing_coverage = $MissingCoverage
        checks           = @($Checks | ForEach-Object { [ordered]@{ tag = $_.Tag; label = $_.Label; count = $_.Lines.Count } })
        coverage         = @($CoverageRows | ForEach-Object { [ordered]@{ name = $_.Name; files = $_.Files; status = $_.Status } })
        findings         = @(foreach ($c in $Checks) { foreach ($ln in $c.Lines) { [ordered]@{ tag = $c.Tag; label = $c.Label; detail = $ln } } })
    }
    $json | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Report -Encoding utf8
    if ($TotalFindings -gt 0 -or $MissingCoverage -gt 0) {
        Write-Host "SOC2 CI: $TotalFindings finding(s), $MissingCoverage missing coverage item(s) - FAIL" -ForegroundColor Red
        exit 1
    }
    Write-Host "SOC2 CI: clean ($TotalFindings findings, $MissingCoverage missing) - PASS"
    exit 0
}

$sb = New-Object System.Text.StringBuilder
if ($Mode -eq 'markdown') {
    [void]$sb.AppendLine("# SOC 2 Type II Compliance Scan Report")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("**Generated:** $(Get-Date)")
    [void]$sb.AppendLine("**Target:** $Target")
    [void]$sb.AppendLine("**Tool:** PowerShell (Select-String; no ripgrep dependency)")
    [void]$sb.AppendLine("**Total findings:** $TotalFindings")
    [void]$sb.AppendLine("**Missing coverage:** $MissingCoverage")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Summary by criterion")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Criterion | Check | Count |")
    [void]$sb.AppendLine("| --------- | ----- | ----- |")
    foreach ($c in $Checks) { [void]$sb.AppendLine("| $($c.Tag) | $($c.Label) | $($c.Lines.Count) |") }
    [void]$sb.AppendLine("")
    foreach ($c in $Checks) {
        [void]$sb.AppendLine("## $($c.Label)  [$($c.Tag)] - $($c.Lines.Count) match(es)")
        [void]$sb.AppendLine("")
        if ($c.Lines.Count -eq 0) { [void]$sb.AppendLine("_No findings._") }
        else { foreach ($ln in $c.Lines) { [void]$sb.AppendLine($ln) } }
        [void]$sb.AppendLine("")
    }
    [void]$sb.AppendLine("## Module & project coverage")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("**Total scanned files:** $TotalScanned")
    [void]$sb.AppendLine("**Missing/empty projects:** $MissingCoverage")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("| Project | Scanned files | Status |")
    [void]$sb.AppendLine("| ------- | ------------- | ------ |")
    foreach ($r in $CoverageRows) { [void]$sb.AppendLine("| $($r.Name) | $($r.Files) | $($r.Status) |") }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Notes")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- Every hit is `path:line:content`; a match is not automatically a violation. Triage each one.")
    [void]$sb.AppendLine("- Secrets, weak crypto, and TLS findings must be verified as env-gated or dev-only before the audit.")
    [void]$sb.AppendLine("- Raw SQL and open-redirect findings are manual-review lists.")
    [void]$sb.AppendLine("- PII findings feed `docs/security/PII_INVENTORY.md`.")
    [void]$sb.AppendLine("- Module coverage asserts all 15 modules + clients/UI/CLI are scanned.")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Report saved to: $Report")
}
else {
    [void]$sb.AppendLine("SOC 2 Compliance Scan Report")
    [void]$sb.AppendLine("Generated: $(Get-Date)")
    [void]$sb.AppendLine("Target:    $Target")
    [void]$sb.AppendLine("Tool:      PowerShell (Select-String; no ripgrep dependency)")
    [void]$sb.AppendLine("Total findings: $TotalFindings")
    [void]$sb.AppendLine("Missing coverage: $MissingCoverage")
    [void]$sb.AppendLine("")
    foreach ($c in $Checks) {
        [void]$sb.AppendLine("====================================================================")
        [void]$sb.AppendLine("$($c.Label)  [$($c.Tag)] - $($c.Lines.Count) match(es)")
        [void]$sb.AppendLine("====================================================================")
        if ($c.Lines.Count -eq 0) { [void]$sb.AppendLine("No findings.") }
        else { foreach ($ln in $c.Lines) { [void]$sb.AppendLine($ln) } }
        [void]$sb.AppendLine("")
    }
    [void]$sb.AppendLine("====================================================================")
    [void]$sb.AppendLine("Module & project coverage")
    [void]$sb.AppendLine("Total scanned files: $TotalScanned")
    [void]$sb.AppendLine("Missing/empty projects: $MissingCoverage")
    foreach ($r in $CoverageRows) { [void]$sb.AppendLine("| $($r.Name) | $($r.Files) | $($r.Status) |") }
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("Report saved to: $Report")
}

$sb.ToString() | Set-Content -LiteralPath $Report -Encoding utf8
Write-Host "SOC2 scan complete: $Report"
exit 0

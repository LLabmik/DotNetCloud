# =============================================================================
# DotNetCloud - SyncTray App Icon Generator
# =============================================================================
# Generates a multi-resolution .ico from a square, transparent PNG source
# (e.g. assets/logo.png). The image is auto-cropped to its content bounds and
# rendered at 16/20/24/32/40/48/64/128/256 px, embedded as PNG-compressed
# entries (supported on Windows Vista and later).
#
# Usage:
#   .\tools\packaging\generate-synctray-icon.ps1 `
#       -Source "assets/logo.png" `
#       -Output "src/Clients/DotNetCloud.Client.SyncTray/Assets/dotnetcloud.ico"
# =============================================================================

param(
    [string]$Source = "",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
if ([string]::IsNullOrEmpty($Source)) { $Source = Join-Path $repoRoot "assets/logo.png" }
if ([string]::IsNullOrEmpty($Output)) {
    $Output = Join-Path $repoRoot "src/Clients/DotNetCloud.Client.SyncTray/Assets/dotnetcloud.ico"
}

$Source = (Resolve-Path $Source).Path

Add-Type -AssemblyName System.Drawing

Write-Host "Reading source: $Source"
$src = [System.Drawing.Bitmap]::FromFile($Source)
if ($src.Width -ne $src.Height) {
    throw "Source image must be square (got $($src.Width)x$($src.Height))."
}

# ── Content bounding box (alpha > 8) ──────────────────────────────────────
$minX = $src.Width; $minY = $src.Height; $maxX = 0; $maxY = 0
for ($y = 0; $y -lt $src.Height; $y++) {
    for ($x = 0; $x -lt $src.Width; $x++) {
        if ($src.GetPixel($x, $y).A -gt 8) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}

if ($maxX -le $minX -or $maxY -le $minY) {
    throw "No opaque content found in source image."
}

# Crop to a centered square around the content with a ~1% padding margin.
$cw = $maxX - $minX + 1
$ch = $maxY - $minY + 1
$side = [Math]::Max($cw, $ch)
$pad = [Math]::Max(2, [int]($side * 0.01))
$side += 2 * $pad
$cx = ($minX + $maxX) / 2.0
$cy = ($minY + $maxY) / 2.0
$cropX = [Math]::Max(0, [int]($cx - $side / 2.0))
$cropY = [Math]::Max(0, [int]($cy - $side / 2.0))
if ($cropX + $side -gt $src.Width) { $cropX = $src.Width - $side }
if ($cropY + $side -gt $src.Height) { $cropY = $src.Height - $side }
Write-Host "Content crop: ($cropX,$cropY) size ${side}x${side}"

# ── Render each size to PNG in memory ─────────────────────────────────────
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$pngs = New-Object System.Collections.Generic.List[object]

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $srcRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $side, $side)
    $dstRect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs.Add([pscustomobject]@{ Size = $s; Data = $ms.ToArray() })
    $ms.Dispose()
    $bmp.Dispose()
}
$src.Dispose()

# ── Assemble ICO (header + directory + PNG blobs) ─────────────────────────
$outDir = Split-Path $Output -Parent
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$fs = [System.IO.File]::Create($Output)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)            # reserved
$bw.Write([uint16]1)            # type = icon
$bw.Write([uint16]$pngs.Count)  # image count

$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $s = $p.Size
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))  # width (0 = 256)
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))  # height
    $bw.Write([byte]0)     # color count
    $bw.Write([byte]0)     # reserved
    $bw.Write([uint16]1)   # color planes
    $bw.Write([uint16]32)  # bits per pixel
    $bw.Write([uint32]$p.Data.Length)  # size of image data
    $bw.Write([uint32]$offset)         # offset of image data
    $offset += $p.Data.Length
}
foreach ($p in $pngs) { $bw.Write($p.Data) }
$bw.Flush()
$bw.Dispose()
$fs.Dispose()

Write-Host "Generated: $Output ($($pngs.Count) sizes: $($sizes -join ','))"

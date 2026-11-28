<#
.SYNOPSIS
Packs the platform contract ("module SDK") packages that modules outside this repository compile
against.

.DESCRIPTION
Optional first-party modules and third-party modules are built in their own repositories, so the
contract assemblies they reference must be available as packages instead of project references.
This script produces that set from this checkout:

    DotNetCloud.Core            interfaces, DTOs, event contracts, capability tiers
    DotNetCloud.Core.Data       EF Core base context, naming strategy, shared entities
    DotNetCloud.Core.Grpc       gRPC interceptor/credential plumbing used by a module host
    DotNetCloud.Modules.Sdk     the supported way to build a sandboxed module host
    DotNetCloud.Modules.Sdk.Web the browser-side half of the module SDK
    DotNetCloud.UI.Shared       Blazor components a module's optional Razor UI reuses

The version is the repository version (`MajorVersion.MinorVersion.PatchVersion` in
Directory.Build.props) unless `-Version` overrides it. Consumers pin it with
`DotNetCloudSdkVersion`, so bump that when this version changes. The release pipeline stamps the
tag, so the SDK a user gets always matches the platform it shipped with.

The packages are pushed by the `Publish Module SDK` workflow (tag `sdk-v*`).

.PARAMETER Output
Directory that receives the .nupkg files. Default: artifacts/module-sdk (gitignored).

.PARAMETER Configuration
Build configuration. Default: Release.

.PARAMETER Version
Version to stamp the packages with. Default: the repository version. The release pipeline passes
the tag so the SDK and the platform carry the same number.

.PARAMETER NoBuild
Pack without rebuilding (the projects must already be built).

.EXAMPLE
./scripts/pack-module-sdk.ps1 -Output ../DotNetCloud-ModuleStore/.sdk-feed
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts\module-sdk'),
    [string]$Configuration = 'Release',
    [string]$Version,
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$contractProjects = @(
    'src/Core/DotNetCloud.Core/DotNetCloud.Core.csproj',
    'src/Core/DotNetCloud.Core.Data/DotNetCloud.Core.Data.csproj',
    'src/Core/DotNetCloud.Core.Grpc/DotNetCloud.Core.Grpc.csproj',
    'src/Core/DotNetCloud.Core.Packaging/DotNetCloud.Core.Packaging.csproj',
    'src/SDK/DotNetCloud.Modules.Sdk/DotNetCloud.Modules.Sdk.csproj',
    'src/SDK/DotNetCloud.Modules.Sdk.Web/DotNetCloud.Modules.Sdk.Web.csproj',
    'src/UI/DotNetCloud.UI.Shared/DotNetCloud.UI.Shared.csproj'
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repoRoot $Output }

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

foreach ($project in $contractProjects) {
    $projectPath = Join-Path $repoRoot $project
    Write-Host "Packing $project ..."

    # Directory.Build.props sets DocumentationFile from $(AssemblyName), which is empty while the
    # pack target evaluates it, so packing otherwise fails with NU5119 (a file named ".xml" cannot be
    # added). The XML doc is a build artifact, not a package asset, so it is cleared for pack only.
    $arguments = @(
        'pack', $projectPath,
        '--configuration', $Configuration,
        '--output', $outputPath,
        '-p:DocumentationFile='
    )
    if (-not [string]::IsNullOrWhiteSpace($Version)) { $arguments += "-p:Version=$Version" }
    if ($NoBuild) { $arguments += '--no-build' }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $project (exit $LASTEXITCODE)." }
}

Write-Host ''
Write-Host "Module SDK packages written to $outputPath"
Get-ChildItem -Path $outputPath -Filter '*.nupkg' | Select-Object -ExpandProperty Name

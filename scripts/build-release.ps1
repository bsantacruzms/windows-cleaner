<#
.SYNOPSIS
    Builds distributable artifacts for Windows Cleaner Tool:
      1. A self-contained publish of the WinUI app (no prerequisites for users)
      2. A portable .zip
      3. A Setup.exe installer (via Inno Setup)
    All artifacts are written to the dist/ folder.

.EXAMPLE
    ./scripts/build-release.ps1
    ./scripts/build-release.ps1 -Version 0.2.0
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Repo root is the parent of the scripts/ folder.
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# Resolve the version from Directory.Build.props when not supplied.
if (-not $Version) {
    $props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
    $Version = if ($props -match "<Version>([^<]+)</Version>") { $Matches[1] } else { "0.1.0" }
}

Write-Host "==> Building Windows Cleaner Tool v$Version ($Configuration)" -ForegroundColor Cyan

$publishDir = Join-Path $root "artifacts\publish"
$distDir = Join-Path $root "dist"
$appProj = Join-Path $root "src\WindowsCleaner.App\WindowsCleaner.App.csproj"

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

# Remove previous release artifacts so only the current single-file exe remains.
Get-ChildItem $distDir -File -ErrorAction SilentlyContinue | Remove-Item -Force

# 1) Publish as a single self-contained .exe. Everything (managed + native + content)
#    is bundled into one file that self-extracts to a temp folder at runtime.
Write-Host "==> Publishing single-file, self-contained win-x64..." -ForegroundColor Cyan
dotnet publish $appProj -c $Configuration -r win-x64 --self-contained true -o $publishDir `
    "/p:Version=$Version" `
    "/p:PublishSingleFile=true" `
    "/p:IncludeNativeLibrariesForSelfExtract=true" `
    "/p:IncludeAllContentForSelfExtract=true" `
    "/p:EnableCompressionInSingleFile=true" `
    "/p:DebugType=none"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# 2) Copy the single exe to dist with a friendly, versioned name.
$sourceExe = Join-Path $publishDir "WindowsCleaner.App.exe"
if (-not (Test-Path $sourceExe)) { throw "Expected single-file output not found: $sourceExe" }
$portableExe = Join-Path $distDir "WindowsCleanerTool-$Version-portable.exe"
Copy-Item $sourceExe $portableExe -Force

Write-Host "`n==> Done. Single portable file in $distDir :" -ForegroundColor Green
Get-ChildItem $distDir -File |
    Select-Object Name, @{ n = "Size"; e = { "{0:N1} MB" -f ($_.Length / 1MB) } } |
    Format-Table -AutoSize

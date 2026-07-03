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

# 1) Publish the app, self-contained for win-x64.
Write-Host "==> Publishing self-contained win-x64..." -ForegroundColor Cyan
dotnet publish $appProj -c $Configuration -r win-x64 --self-contained true -o $publishDir "/p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# 2) Portable zip.
$zipPath = Join-Path $distDir "WindowsCleanerTool-$Version-win-x64-portable.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Write-Host "==> Creating portable zip..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

# 3) Installer via Inno Setup.
$iscc = Get-ChildItem `
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", `
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe", `
    "C:\Program Files\Inno Setup 6\ISCC.exe" `
    -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $iscc) {
    throw "ISCC.exe (Inno Setup) not found. Install it with: winget install JRSoftware.InnoSetup"
}

$iss = Join-Path $root "installer\WindowsCleanerTool.iss"
Write-Host "==> Compiling installer with Inno Setup..." -ForegroundColor Cyan
& $iscc.FullName "/DMyAppVersion=$Version" $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }

Write-Host "`n==> Done. Artifacts in $distDir :" -ForegroundColor Green
Get-ChildItem $distDir -File |
    Select-Object Name, @{ n = "Size"; e = { "{0:N1} MB" -f ($_.Length / 1MB) } } |
    Format-Table -AutoSize

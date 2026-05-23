# Deploy-Mod.ps1 — Copies built mod to BepInEx plugins folder
param(
    [string]$GameDir = "C:\Program Files (x86)\alphatest\Six Ages 2 Lights Going Out"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$BuildDir = Join-Path $ProjectRoot "build"
$PluginsDir = Join-Path $GameDir "BepInEx\plugins\SixAgesAccessibility"

$modDll = Join-Path $BuildDir "SixAgesAccessibility.dll"

if (-not (Test-Path $modDll)) {
    Write-Host "Mod DLL not found. Run Build-Mod.ps1 first." -ForegroundColor Red
    exit 1
}

# Create plugins subfolder
if (-not (Test-Path $PluginsDir)) {
    New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
}

# Copy mod DLL
Copy-Item $modDll $PluginsDir -Force
Write-Host "Deployed: $modDll -> $PluginsDir" -ForegroundColor Green

# Check for Tolk DLLs in game directory
$tolkDll = Join-Path $GameDir "Tolk.dll"
$nvdaDll = Join-Path $GameDir "nvdaControllerClient32.dll"

if (-not (Test-Path $tolkDll)) {
    Write-Host "WARNING: Tolk.dll not found in game directory. Screen reader output will be disabled." -ForegroundColor Yellow
    Write-Host "  Copy Tolk.dll to: $GameDir" -ForegroundColor Yellow
}

if (-not (Test-Path $nvdaDll)) {
    Write-Host "WARNING: nvdaControllerClient32.dll not found. NVDA support requires this file." -ForegroundColor Yellow
    Write-Host "  Copy nvdaControllerClient32.dll to: $GameDir" -ForegroundColor Yellow
}

Write-Host "Deploy complete. Start the game to test." -ForegroundColor Cyan

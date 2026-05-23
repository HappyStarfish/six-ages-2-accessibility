# Package-AccessibilityOnly.ps1 - Builds the accessibility mod and bundles a ZIP.
#
# Result mirrors the Six Ages 2 game folder structure - recipient extracts into
# the game directory, merges/replaces, the mod loads on next launch.
#
# Pass -GameDir <path> or set $env:SIXAGES_GAME_DIR to point at your installed
# Six Ages 2 folder (needed for the bundled BepInEx core and Tolk DLLs).
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$GameDir = $env:SIXAGES_GAME_DIR,
    [string]$Version = (Get-Date -Format "yyyy-MM-dd"),
    [string]$OutputDir,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

if (-not $GameDir) {
    throw "GameDir not set. Pass -GameDir <path> or set `$env:SIXAGES_GAME_DIR to point at your installed Six Ages 2 folder."
}

$ProjectRoot = Split-Path $PSScriptRoot -Parent
$BuildDir = Join-Path $ProjectRoot "build"
if (-not $OutputDir) { $OutputDir = Join-Path $ProjectRoot "dist" }
$StageDir = Join-Path $OutputDir "staging-accessibility"
$ZipPath = Join-Path $OutputDir "SixAges2-Accessibility_$Version.zip"

Write-Host "=== Package Accessibility-Only - $Configuration, v$Version ===" -ForegroundColor Cyan

# --- 1. Build accessibility mod --------------------------------------------
if ($SkipBuild) {
    Write-Host "Skipping build (-SkipBuild) - using existing build\ output." -ForegroundColor Yellow
} else {
    & (Join-Path $PSScriptRoot "Build-Mod.ps1") -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build-Mod.ps1 failed - aborting." }
}

# --- 2. Verify game directory ----------------------------------------------
if (-not (Test-Path $GameDir)) {
    throw "Game directory not found: $GameDir`nPass -GameDir <path> to point at your LGO install."
}

# --- 3. Collect source files (source path -> path inside the archive) ------
$files = @(
    @{ Src = (Join-Path $GameDir "winhttp.dll");                Dst = "winhttp.dll" }
    @{ Src = (Join-Path $GameDir "doorstop_config.ini");        Dst = "doorstop_config.ini" }
    @{ Src = (Join-Path $GameDir "Tolk.dll");                   Dst = "Tolk.dll" }
    @{ Src = (Join-Path $GameDir "nvdaControllerClient32.dll"); Dst = "nvdaControllerClient32.dll" }
    @{ Src = (Join-Path $BuildDir "SixAgesAccessibility.dll");  Dst = "BepInEx\plugins\SixAgesAccessibility\SixAgesAccessibility.dll" }
    @{ Src = (Join-Path $ProjectRoot "README.md");              Dst = "README.md" }
)

$missing = @()
foreach ($f in $files) {
    if (-not (Test-Path $f.Src)) { $missing += $f.Src }
}
$coreSrc = Join-Path $GameDir "BepInEx\core"
if (-not (Test-Path $coreSrc)) { $missing += $coreSrc }
if ($missing.Count -gt 0) {
    Write-Host "Missing required source files:" -ForegroundColor Red
    foreach ($m in $missing) { Write-Host "  $m" -ForegroundColor Red }
    throw "Cannot package - see missing files above."
}

# --- 4. Build a clean staging folder ---------------------------------------
if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Path $StageDir -Force | Out-Null

function Copy-Into {
    param([string]$Source, [string]$RelativeDest)
    $target = Join-Path $StageDir $RelativeDest
    $targetDir = Split-Path $target -Parent
    if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
    Copy-Item $Source $target -Force
}

foreach ($f in $files) {
    Copy-Into -Source $f.Src -RelativeDest $f.Dst
    Write-Host "  + $($f.Dst)" -ForegroundColor DarkGray
}

# BepInEx core (whole folder, .xml docs excluded - not needed at runtime).
$coreDst = Join-Path $StageDir "BepInEx\core"
New-Item -ItemType Directory -Path $coreDst -Force | Out-Null
Copy-Item (Join-Path $coreSrc "*") $coreDst -Recurse -Force -Exclude *.xml
Write-Host "  + BepInEx\core\ (loader, .xml docs excluded)" -ForegroundColor DarkGray

# BepInEx.cfg - optional; bundling it guarantees identical loader behaviour.
$cfgSrc = Join-Path $GameDir "BepInEx\config\BepInEx.cfg"
if (Test-Path $cfgSrc) {
    Copy-Into -Source $cfgSrc -RelativeDest "BepInEx\config\BepInEx.cfg"
    Write-Host "  + BepInEx\config\BepInEx.cfg" -ForegroundColor DarkGray
} else {
    Write-Host "  ~ BepInEx.cfg not found - recipient's game generates it on first launch." -ForegroundColor Yellow
}

# --- 5. Compress -----------------------------------------------------------
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $StageDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal

# --- 6. Clean up staging ---------------------------------------------------
Remove-Item $StageDir -Recurse -Force

$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Accessibility-only release package created:" -ForegroundColor Green
Write-Host "  $ZipPath ($zipSize MB)" -ForegroundColor Green
Write-Host ""
Write-Host "Recipient: extract this ZIP into the LGO game folder, merging folders /" -ForegroundColor Cyan
Write-Host "replacing files when prompted. Accessibility mod loads on next game launch." -ForegroundColor Cyan

# Build-Mod.ps1 — Builds the Six Ages 2 Accessibility Mod
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$ProjectFile = Join-Path $ProjectRoot "src\SixAgesAccessibility\SixAgesAccessibility.csproj"
$BuildDir = Join-Path $ProjectRoot "build"

Write-Host "Building SixAgesAccessibility ($Configuration)..." -ForegroundColor Cyan

dotnet build $ProjectFile -c $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}

$dll = Join-Path $BuildDir "SixAgesAccessibility.dll"
if (Test-Path $dll) {
    $size = (Get-Item $dll).Length
    Write-Host "Build OK: $dll ($size bytes)" -ForegroundColor Green
} else {
    Write-Host "Build completed but DLL not found at $dll" -ForegroundColor Yellow
}

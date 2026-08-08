<#
.SYNOPSIS
  Build the distributable Solar Shading installer package.

.DESCRIPTION
  Builds the add-in for each supported runtime, stages a self-contained payload
  (binaries + Install/Uninstall scripts + readme), and produces:

    dist/SolarShading-<version>.zip          always
    dist/SolarShading-<version>-Setup.exe    only if Inno Setup (ISCC.exe) is installed

  Building for a Revit version requires that Revit's RevitAPI.dll is present on
  this machine; versions that are missing are skipped with a warning.

  net8  payload  -> Revit 2025 / 2026   (built against the Revit 2026 API)
  net10 payload  -> Revit 2027

.EXAMPLE
  .\Build-Package.ps1
  .\Build-Package.ps1 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = Split-Path -Parent $here
$project = Join-Path $root 'src\SolarShading.Revit\SolarShading.Revit.csproj'

# payload name -> the Revit version whose API we compile against
$payloads = [ordered]@{ 'net8' = '2026'; 'net10' = '2027' }

$stage = Join-Path $here "obj\SolarShading-$Version"
$dist = Join-Path $root 'dist'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage, $dist | Out-Null

$built = @()
foreach ($p in $payloads.GetEnumerator()) {
    $payload = $p.Key
    $revit = $p.Value
    $api = Join-Path $env:ProgramFiles "Autodesk\Revit $revit\RevitAPI.dll"
    if (-not (Test-Path $api)) {
        Write-Warning "Revit $revit API not found - '$payload' payload skipped (install Revit $revit to include it)."
        continue
    }

    Write-Host "Building $payload payload (Revit $revit API, $Configuration)..." -ForegroundColor Cyan
    dotnet build $project -c $Configuration -p:RevitVersion=$revit -p:Version=$Version --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed for Revit $revit." }

    $outDir = Join-Path $root "src\SolarShading.Revit\bin\$Configuration"
    $target = Join-Path $stage "bin\$payload"
    New-Item -ItemType Directory -Force -Path $target | Out-Null

    # Ship only what Revit needs to load the add-in (no pdbs, no Revit API copies).
    Get-ChildItem -Path $outDir -Filter *.dll |
        Where-Object { $_.Name -notlike 'RevitAPI*' } |
        ForEach-Object { Copy-Item $_.FullName -Destination $target -Force }

    $built += $payload
}
if ($built.Count -eq 0) { throw "No payload could be built - no supported Revit API found." }

Copy-Item (Join-Path $here 'Install.ps1')   -Destination $stage -Force
Copy-Item (Join-Path $here 'Uninstall.ps1') -Destination $stage -Force
Copy-Item (Join-Path $root 'LICENSE')       -Destination $stage -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'USER_GUIDE.md') -Destination $stage -Force -ErrorAction SilentlyContinue

@"
Solar Shading $Version - Revit add-in
=====================================

Envelope solar shading and ETTV/OTTV compliance for Revit 2025-2027.

INSTALL
  1. Close Revit.
  2. Right-click Install.ps1 -> "Run with PowerShell".
     (or in PowerShell:  .\Install.ps1 )
  3. Start Revit and open the "Solar Shading" ribbon tab.

  If Windows blocks the script, run this once in PowerShell:
     Set-ExecutionPolicy -Scope Process -Bypass

  Options:
     .\Install.ps1 -RevitVersion 2026      install for one version only
     .\Install.ps1 -AllUsers               install for everyone (run as Administrator)

FIRST RUN
  Click "Setup Parameters" once per project to create the SS_* shared parameters.
  See USER_GUIDE.md for the full workflow.

UNINSTALL
  .\Uninstall.ps1        (add -AllUsers if you installed for all users)

Project page: https://github.com/KenLP/RevitSolarShading
"@ | Set-Content -Path (Join-Path $stage 'README.txt') -Encoding UTF8

$zip = Join-Path $dist "SolarShading-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host "ZIP:  $zip" -ForegroundColor Green

# Optional: compile a real .exe installer when Inno Setup is available.
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source }

if ($iscc) {
    Write-Host "Compiling setup .exe with Inno Setup..." -ForegroundColor Cyan
    & $iscc "/DMyAppVersion=$Version" "/DStageDir=$stage" "/O$dist" (Join-Path $here 'SolarShading.iss')
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }
    Write-Host "EXE:  $dist\SolarShading-$Version-Setup.exe" -ForegroundColor Green
}
else {
    Write-Host "Inno Setup not found - skipped the .exe (the ZIP is a complete installer)." -ForegroundColor DarkYellow
    Write-Host "To also build a setup .exe, install Inno Setup 6 and re-run this script." -ForegroundColor DarkYellow
}

Write-Host "Payloads packaged: $($built -join ', ')" -ForegroundColor Green

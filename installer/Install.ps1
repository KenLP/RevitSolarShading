<#
.SYNOPSIS
  Install the Solar Shading Revit add-in.

.DESCRIPTION
  Copies the add-in binaries into the Revit Addins folder and writes the .addin
  manifest for every Revit version you choose. No build tools required — this
  script installs the pre-built binaries shipped next to it.

  By default it installs for the current user (%APPDATA%). Use -AllUsers to
  install for everyone on the machine (requires an elevated PowerShell).

  Close Revit before installing.

.PARAMETER RevitVersion
  One or more Revit versions to install for (2025, 2026, 2027).
  Omit to auto-detect every supported Revit installed on this machine.

.PARAMETER AllUsers
  Install into %PROGRAMDATA% for all users instead of the current user.
  Run PowerShell as Administrator when using this.

.EXAMPLE
  .\Install.ps1
  .\Install.ps1 -RevitVersion 2026
  .\Install.ps1 -RevitVersion 2026,2027 -AllUsers
#>
[CmdletBinding()]
param(
    [ValidateSet('2025', '2026', '2027')]
    [string[]]$RevitVersion,
    [switch]$AllUsers
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot

# Revit 2025/2026 run on .NET 8; Revit 2027 runs on .NET 10.
$payloadFor = @{ '2025' = 'net8'; '2026' = 'net8'; '2027' = 'net10' }

$addInId = '7e6a2c64-9b1d-4f0a-8c3e-2f5d9a1b4c70'
$manifest = @'
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Solar Shading</Name>
    <Assembly>SolarShading\SolarShading.Revit.dll</Assembly>
    <AddInId>{0}</AddInId>
    <FullClassName>SolarShading.Revit.App</FullClassName>
    <VendorId>SSHD</VendorId>
    <VendorDescription>Solar Shading / ETTV tools</VendorDescription>
  </AddIn>
</RevitAddIns>
'@ -f $addInId

function Get-InstalledRevitVersions {
    $found = @()
    foreach ($v in @('2025', '2026', '2027')) {
        $exe = Join-Path $env:ProgramFiles "Autodesk\Revit $v\Revit.exe"
        if (Test-Path $exe) { $found += $v }
    }
    return $found
}

if (Get-Process -Name 'Revit' -ErrorAction SilentlyContinue) {
    throw "Revit is running. Close Revit and run this installer again."
}

if (-not $RevitVersion -or $RevitVersion.Count -eq 0) {
    $RevitVersion = Get-InstalledRevitVersions
    if ($RevitVersion.Count -eq 0) {
        throw "No supported Revit (2025-2027) found. Install Revit first, or pass -RevitVersion explicitly."
    }
    Write-Host "Detected Revit: $($RevitVersion -join ', ')" -ForegroundColor Cyan
}

if ($AllUsers) {
    $addinsBase = Join-Path $env:ProgramData 'Autodesk\Revit\Addins'
    $isAdmin = ([Security.Principal.WindowsPrincipal] `
        [Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        throw "-AllUsers needs an elevated PowerShell. Re-run as Administrator, or drop -AllUsers for a per-user install."
    }
}
else {
    $addinsBase = Join-Path $env:APPDATA 'Autodesk\Revit\Addins'
}

$installed = 0
foreach ($v in $RevitVersion) {
    $payload = Join-Path $here "bin\$($payloadFor[$v])"
    if (-not (Test-Path $payload)) {
        Write-Warning "Revit ${v}: payload '$($payloadFor[$v])' is missing from this package - skipped."
        continue
    }

    $versionRoot = Join-Path $addinsBase $v
    $installDir = Join-Path $versionRoot 'SolarShading'
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    Get-ChildItem -Path $payload -File | ForEach-Object {
        Copy-Item $_.FullName -Destination $installDir -Force
    }
    Set-Content -Path (Join-Path $versionRoot 'SolarShading.addin') -Value $manifest -Encoding UTF8

    Write-Host "Installed for Revit ${v}: $installDir" -ForegroundColor Green
    $installed++
}

if ($installed -eq 0) { throw "Nothing was installed." }

Write-Host ""
Write-Host "Done. Start Revit and open the 'Solar Shading' ribbon tab." -ForegroundColor Green
Write-Host "First run: click 'Setup Parameters' once per project." -ForegroundColor Green

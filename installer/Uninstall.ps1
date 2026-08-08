<#
.SYNOPSIS
  Remove the Solar Shading Revit add-in.

.DESCRIPTION
  Deletes the add-in binaries and the .addin manifest for the chosen Revit
  versions. Your model data and the SS_* shared parameters already written into
  projects are NOT touched.

  Close Revit before uninstalling.

.EXAMPLE
  .\Uninstall.ps1
  .\Uninstall.ps1 -RevitVersion 2026 -AllUsers
#>
[CmdletBinding()]
param(
    [ValidateSet('2025', '2026', '2027')]
    [string[]]$RevitVersion = @('2025', '2026', '2027'),
    [switch]$AllUsers
)

$ErrorActionPreference = 'Stop'

if (Get-Process -Name 'Revit' -ErrorAction SilentlyContinue) {
    throw "Revit is running. Close Revit and run this uninstaller again."
}

$addinsBase = if ($AllUsers) {
    Join-Path $env:ProgramData 'Autodesk\Revit\Addins'
} else {
    Join-Path $env:APPDATA 'Autodesk\Revit\Addins'
}

$removed = 0
foreach ($v in $RevitVersion) {
    $versionRoot = Join-Path $addinsBase $v
    $installDir = Join-Path $versionRoot 'SolarShading'
    $addinFile = Join-Path $versionRoot 'SolarShading.addin'

    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force
        Write-Host "Removed $installDir" -ForegroundColor Yellow
        $removed++
    }
    if (Test-Path $addinFile) {
        Remove-Item $addinFile -Force
        Write-Host "Removed $addinFile" -ForegroundColor Yellow
        $removed++
    }
}

if ($removed -eq 0) {
    Write-Host "Nothing to remove (add-in not installed in that scope)." -ForegroundColor Cyan
} else {
    Write-Host "Solar Shading uninstalled." -ForegroundColor Green
}

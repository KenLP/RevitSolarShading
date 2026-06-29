<#
.SYNOPSIS
  Build the Solar Shading add-in and install it for a given Revit version.

.DESCRIPTION
  Builds SolarShading.Revit for the requested Revit version (net8.0 for 2025/2026,
  net10.0 for 2027), then copies the output DLLs into
  %APPDATA%\Autodesk\Revit\Addins\<version>\SolarShading\ and writes the .addin
  manifest pointing at them. Revit must be closed while deploying.

.EXAMPLE
  ./Deploy.ps1 -RevitVersion 2026
  ./Deploy.ps1 -RevitVersion 2027 -Configuration Debug
#>
param(
    [ValidateSet('2025', '2026', '2027')]
    [string]$RevitVersion = '2026',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/SolarShading.Revit/SolarShading.Revit.csproj'

Write-Host "Building SolarShading.Revit for Revit $RevitVersion ($Configuration)..." -ForegroundColor Cyan
dotnet build $project -c $Configuration -p:RevitVersion=$RevitVersion
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# Output folder (AppendTargetFrameworkToOutputPath=false => no TFM subfolder).
$outDir = Join-Path $root "src/SolarShading.Revit/bin/$Configuration"
if (-not (Test-Path $outDir)) { throw "Build output not found: $outDir" }

$addinsRoot = Join-Path $env:APPDATA "Autodesk/Revit/Addins/$RevitVersion"
$installDir = Join-Path $addinsRoot 'SolarShading'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

Write-Host "Copying DLLs to $installDir" -ForegroundColor Cyan
Get-ChildItem -Path $outDir -Filter *.dll | ForEach-Object {
    Copy-Item $_.FullName -Destination $installDir -Force
}
# Also copy the pdbs for debugging convenience.
Get-ChildItem -Path $outDir -Filter *.pdb -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName -Destination $installDir -Force
}

# Write the manifest at the Addins root, pointing into the subfolder.
$addinPath = Join-Path $addinsRoot 'SolarShading.addin'
$manifest = @'
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Solar Shading</Name>
    <Assembly>SolarShading\SolarShading.Revit.dll</Assembly>
    <AddInId>7e6a2c64-9b1d-4f0a-8c3e-2f5d9a1b4c70</AddInId>
    <FullClassName>SolarShading.Revit.App</FullClassName>
    <VendorId>SSHD</VendorId>
    <VendorDescription>Solar Shading / ETTV tools</VendorDescription>
  </AddIn>
</RevitAddIns>
'@
Set-Content -Path $addinPath -Value $manifest -Encoding UTF8

Write-Host "Installed:" -ForegroundColor Green
Write-Host "  Manifest: $addinPath"
Write-Host "  Binaries: $installDir"
Write-Host "Start Revit $RevitVersion and look for the 'Solar Shading' ribbon tab." -ForegroundColor Green

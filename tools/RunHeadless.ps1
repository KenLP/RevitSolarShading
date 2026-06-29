<#
.SYNOPSIS
  Trigger a headless Solar Shading run in a running Revit and wait for the result.

.DESCRIPTION
  Writes a trigger JSON into %TEMP%\SolarShading\trigger.json. The add-in's idle watcher
  picks it up, runs the analysis on the active document, and writes result.json. Revit must
  be open with the model and the Solar Shading add-in loaded.

.EXAMPLE
  ./RunHeadless.ps1 -ConfigJson '{ "months":[6], "startHour":7, "endHour":18, "showOverlay":false }'
  ./RunHeadless.ps1 -ConfigPath my.json -TimeoutSec 120
#>
param(
    [string]$ConfigJson = '{ "months":[3,6,12], "startHour":7, "endHour":18, "showOverlay":false, "writeParameters":true, "exportCsv":false }',
    [string]$ConfigPath,
    [int]$TimeoutSec = 180
)

# Must match HeadlessRunner.Dir (LocalApplicationData, not TEMP — Revit redirects TEMP).
$dir = Join-Path $env:LOCALAPPDATA 'SolarShading'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$trigger = Join-Path $dir 'trigger.json'
$result = Join-Path $dir 'result.json'

if ($ConfigPath) { $ConfigJson = Get-Content -Raw -Path $ConfigPath }

Remove-Item $result -ErrorAction SilentlyContinue
Set-Content -Path $trigger -Value $ConfigJson -Encoding UTF8
Write-Host "Trigger written. Waiting for Revit to process (timeout ${TimeoutSec}s)..." -ForegroundColor Cyan

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $result) {
        Start-Sleep -Milliseconds 200
        Get-Content -Raw -Path $result
        exit 0
    }
    Start-Sleep -Milliseconds 500
}
Write-Host "Timed out. Is Revit open with the model and the add-in loaded?" -ForegroundColor Yellow
exit 1

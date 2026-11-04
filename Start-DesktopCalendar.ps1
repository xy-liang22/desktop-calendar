param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$appExe = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\DesktopCalendar.exe"
if (-not (Test-Path -LiteralPath $appExe)) {
    throw 'Build first: powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1'
}
Start-Process -FilePath $appExe -WorkingDirectory $PSScriptRoot -WindowStyle Normal

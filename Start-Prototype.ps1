param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$prototypeExe = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\DesktopCalendar.exe"
if (-not (Test-Path -LiteralPath $prototypeExe)) {
    throw 'Build first: powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1'
}
Start-Process -FilePath $prototypeExe -WorkingDirectory $PSScriptRoot -WindowStyle Normal

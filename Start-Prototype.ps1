$ErrorActionPreference = 'Stop'
$prototypeExe = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\DesktopCalendar.exe'
if (-not (Test-Path -LiteralPath $prototypeExe)) {
    throw 'Build first: powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1'
}
Start-Process -FilePath $prototypeExe -WorkingDirectory $PSScriptRoot -WindowStyle Normal

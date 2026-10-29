param([switch]$Test)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'DesktopCalendar-DotnetCli'
$dotnetExe = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
$project = Join-Path $PSScriptRoot 'DesktopCalendar.csproj'
& $dotnetExe restore $project --configfile (Join-Path $PSScriptRoot 'NuGet.Config') -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Offline restore failed.' }
& $dotnetExe build $project --no-restore -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$shortcutShell = New-Object -ComObject WScript.Shell
$shortcut = $shortcutShell.CreateShortcut((Join-Path $PSScriptRoot 'Desktop Calendar P1.lnk'))
$shortcut.TargetPath = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\DesktopCalendar.exe'
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.Description = 'Desktop Calendar P1'
$shortcut.Save()
if ($Test) {
    $exePath = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows\DesktopCalendar.exe'
    $testProcess = Start-Process -FilePath $exePath -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
    Get-Content (Join-Path $PSScriptRoot 'artifacts\p1-checks.txt') -Encoding UTF8
    if ($testProcess.ExitCode -ne 0) { throw 'P1 checks failed.' }
}

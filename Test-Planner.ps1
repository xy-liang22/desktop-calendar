param([string]$ReportDirectory = 'artifacts\p2-checks')
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'DesktopCalendar-DotnetCli'
$report = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $ReportDirectory))
$allowed = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts')) + '\'
if (-not $report.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Reports/test data must remain under project artifacts.' }
Push-Location $PSScriptRoot
try {
    & (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe') run --project tests\PlannerChecks -c Release -- $report
    if ($LASTEXITCODE -ne 0) { throw 'Planner verification failed. See artifacts/p2-checks/checks.txt.' }
} finally { Pop-Location }

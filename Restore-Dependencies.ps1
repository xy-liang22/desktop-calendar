param()
$ErrorActionPreference = 'Stop'
# Explicit opt-in download only. Ordinary build/test scripts still use the empty offline feed.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'DesktopCalendar-DotnetCli'
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
foreach ($project in @('DesktopCalendar.csproj', 'tests\PlannerChecks\PlannerChecks.csproj')) {
    & $dotnet restore (Join-Path $PSScriptRoot $project) --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'Approved dependency restore failed; no application data changed.' }
}
$assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Core\obj\project.assets.json') -Raw | ConvertFrom-Json
if ($assets.libraries.'lunar-csharp/1.6.8'.sha512 -ne 'Mo0lruiaalOsT2pF/lcykrJ8/qFWZo6JxF8U1hdpZklGeuNLUa3p6BPcyFjLeXuIn1r1PXX87+fSFdxtkGrhjg==' -or @($assets.libraries.PSObject.Properties).Count -ne 1) { throw 'Approved dependency identity differs; stop before build.' }

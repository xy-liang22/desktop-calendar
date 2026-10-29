param([switch]$Test, [switch]$BuildOnly)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'DesktopCalendar-DotnetCli'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sdk = Join-Path $env:ProgramFiles 'dotnet/dotnet.exe'
$project = Join-Path $PSScriptRoot 'DesktopHostProbe.csproj'
& $sdk restore $project --configfile (Join-Path $projectRoot 'NuGet.Config') -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Offline restore failed' }
& $sdk build $project --no-restore -c Release
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
$exe = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/DesktopHostProbe.exe'
if ($Test) {
    foreach ($testCase in @(@('--self-test','p1.1-2-checks.txt'), @('--supervisor-test','p1.1-2-supervisor-checks.txt'))) {
        $startedAt = [DateTime]::UtcNow
        $process = Start-Process -FilePath $exe -ArgumentList $testCase[0] -WorkingDirectory $projectRoot -WindowStyle Hidden -Wait -PassThru
        $report = Join-Path $projectRoot ('artifacts/' + $testCase[1])
        if (!(Test-Path $report) -or (Get-Item $report).LastWriteTimeUtc -lt $startedAt) { throw 'No fresh test report; checks did not finish' }
        Get-Content $report -Encoding UTF8
        if ($process.ExitCode -ne 0 -or (Select-String -Path $report -Pattern '^FAIL ' -Quiet)) { throw "Probe checks failed (exit $($process.ExitCode)); see report above" }
    }
} elseif (!$BuildOnly) {
    # Explicitly requested interactive test application; the controller must remain visible.
    Start-Process -FilePath $exe -WorkingDirectory $projectRoot
}

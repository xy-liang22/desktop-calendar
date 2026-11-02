param([switch]$SkipBuild, [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Configuration = 'P3')
$ErrorActionPreference = 'Stop'
if (-not $SkipBuild) { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration; if ($LASTEXITCODE -ne 0) { throw 'Build failed.' } }
$root = Join-Path $PSScriptRoot ('artifacts\p3-checks\run-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
$exe = Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\DesktopCalendar.exe"
function Run($phase, $directory) {
    $info = [Diagnostics.ProcessStartInfo]::new($exe, '--planner-check ' + $phase)
    $info.UseShellExecute = $false; $info.EnvironmentVariables['DESKTOP_CALENDAR_P3_CHECK_ROOT'] = $directory
    $process = [Diagnostics.Process]::Start($info)
    try { if (-not $process.WaitForExit(45000)) { $process.Kill(); throw "Own check process timed out: $phase" }; if ($process.ExitCode -ne 0) { throw "P3 check failed: $phase; reports: $directory" } } finally { $process.Dispose() }
}
Run '' $root
Run '--verify-restart' $root
$corrupt = Join-Path $root 'corrupt'; [IO.Directory]::CreateDirectory($corrupt) | Out-Null
[IO.File]::Copy((Join-Path $root 'planner-data.json'), (Join-Path $corrupt 'planner-data.json.bak'))
[IO.File]::WriteAllText((Join-Path $corrupt 'planner-data.json'), 'BROKEN-P3-FIXTURE')
Run '--verify-corrupt' $corrupt
$newer = Join-Path $root 'newer'; [IO.Directory]::CreateDirectory($newer) | Out-Null
[IO.File]::Copy((Join-Path $root 'planner-data.json'), (Join-Path $newer 'planner-data.json.bak'))
[IO.File]::WriteAllText((Join-Path $newer 'planner-data.json'), '{"schemaVersion":999,"futureField":"test fixture"}')
Run '--verify-newer' $newer
$checks = @(Get-ChildItem -LiteralPath $root -Filter 'checks-*.txt' -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Encoding UTF8 })
Write-Output ('PASS: ' + @($checks | Where-Object { $_ -match '^PASS ' }).Count)
$checks | Where-Object { $_ -match '^FAIL ' }
Write-Output "Reports: $root"

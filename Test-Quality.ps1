param([Parameter(Mandatory=$true)][string]$FixtureRoot, [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Configuration='P9', [ValidateRange(180,86400)][int]$DurationSeconds=180)
$ErrorActionPreference='Stop'
$allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts'))+'\'
$FixtureRoot=[IO.Path]::GetFullPath($FixtureRoot)
if (-not $FixtureRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Quality checks only accept project artifacts fixtures.' }
$root=Join-Path $PSScriptRoot ('artifacts\quality-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
foreach($name in 'planner-data.json','window-preferences.json','calendar-knowledge.json') { $file=Join-Path $FixtureRoot $name; if(Test-Path -LiteralPath $file){Copy-Item -LiteralPath $file -Destination (Join-Path $root $name)} }
$images=Join-Path $FixtureRoot 'assets\images'
if(Test-Path -LiteralPath $images) { [IO.Directory]::CreateDirectory((Join-Path $root 'assets')) | Out-Null; Copy-Item -LiteralPath $images -Destination (Join-Path $root 'assets\images') -Recurse }
$exe=Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\DesktopCalendar.exe"
$info=[Diagnostics.ProcessStartInfo]::new($exe,'--planner-check --verify-soak');$info.UseShellExecute=$false;$info.EnvironmentVariables['DESKTOP_CALENDAR_P3_CHECK_ROOT']=$root
$info.EnvironmentVariables['DESKTOP_CALENDAR_SOAK_SECONDS']=[string]$DurationSeconds
$process=[Diagnostics.Process]::Start($info)
try { if(-not $process.WaitForExit(($DurationSeconds+60)*1000)) { $process.Kill(); throw "Own soak check timed out; evidence: $root" }; if($process.ExitCode -ne 0){throw "Soak check failed: $root"} } finally {$process.Dispose()}
Get-Content -LiteralPath (Join-Path $root 'p9-soak-summary.json') -Encoding UTF8
Write-Output "Quality report: $root"

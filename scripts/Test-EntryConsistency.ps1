param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Configuration='P9')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$output=Join-Path $project ('artifacts\entry-checks-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($output)|Out-Null
$package=Get-Content -LiteralPath (Join-Path $project 'artifacts\package\package.json') -Raw -Encoding UTF8|ConvertFrom-Json
$root=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\DesktopCalendar'
$state=Get-Content -LiteralPath (Join-Path $root 'installation.json') -Raw -Encoding UTF8|ConvertFrom-Json
if($state.AppId -ne 'desktop-calendar' -or $state.Current -notmatch '^versions\\v[a-f0-9]{16}$'){throw 'Unmanaged install state.'}
$installed=[IO.Path]::GetFullPath((Join-Path $root $state.Current)); if(-not $installed.StartsWith([IO.Path]::GetFullPath($root)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Install path escaped.'}
$results=[Collections.Generic.List[string]]::new()
function Check($name,$ok){$results.Add($(if($ok){'PASS '}else{'FAIL '})+$name)}
$names=@('DesktopCalendar.exe','DesktopCalendar.dll','DesktopCalendar.Core.dll','DesktopCalendar.deps.json','DesktopCalendar.runtimeconfig.json')
if($package.Files.Count -ne 5 -or @($package.Files | Where-Object {$_.Name -notin $names}).Count){throw 'Unexpected package manifest.'}
foreach($file in $package.Files){$dev=Join-Path $project "bin\$Configuration\net10.0-windows\$($file.Name)";$deployed=Join-Path $installed $file.Name
 Check ('identical developer/package/installed bytes '+$file.Name) ((Get-FileHash -LiteralPath $dev -Algorithm SHA256).Hash -eq $file.Sha256 -and (Get-FileHash -LiteralPath $deployed -Algorithm SHA256).Hash -eq $file.Sha256)}
Check 'candidate and installed versions match' ($state.Version -eq $package.Version -and ([version](Get-Item (Join-Path $installed 'DesktopCalendar.dll')).VersionInfo.FileVersion).ToString(3) -eq $package.Version)
$exe=Join-Path $installed 'DesktopCalendar.exe';$shell=New-Object -ComObject WScript.Shell;$link=$shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Desktop Calendar.lnk'))
Check 'actual current-user Start Menu targets current installed candidate' ($link.TargetPath -eq $exe -and $link.Arguments -eq '')
Check 'actual Start Menu uses official EXE icon' ($link.IconLocation -eq ($exe+',0'))
# The actual installed EXE is exercised only with an explicit isolated lifecycle root, never personal planner data.
$info=[Diagnostics.ProcessStartInfo]::new($exe,'--lifecycle-check=installed');$info.UseShellExecute=$false;$info.EnvironmentVariables['DESKTOP_CALENDAR_CHECK_ROOT']=$output
$process=[Diagnostics.Process]::Start($info)
try {if(-not $process.WaitForExit(20000)){$process.Kill();throw 'Own installed smoke child timed out.'};$smoke=Get-Content -LiteralPath (Join-Path $output 'installed.json') -Raw -Encoding UTF8|ConvertFrom-Json
 Check 'actual installed candidate opens isolated branded desktop HWND and exits' ($process.ExitCode -eq 0 -and $smoke.Ready -and $smoke.DesktopOwner -and $smoke.Brand -and $smoke.Executable -eq $exe)}finally{$process.Dispose()}
[IO.File]::WriteAllLines((Join-Path $output 'checks.txt'),$results,[Text.UTF8Encoding]::new($false))
$fail=@($results|Where-Object {$_ -like 'FAIL *'}).Count;Write-Output ('Entry checks PASS='+@($results|Where-Object {$_ -like 'PASS *'}).Count+' FAIL='+$fail);Write-Output "Entry report: $output"
if($fail){$results|Where-Object {$_ -like 'FAIL *'};throw 'Entry consistency failed.'}

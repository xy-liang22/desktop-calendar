param([ValidateSet('Status','Update','Import')][string]$Action='Status', [switch]$Online, [string]$PackagePath, [string]$DataRoot, [string]$Configuration='Release')
$ErrorActionPreference='Stop'
if ($Configuration -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw 'Invalid configuration.' }
if ($Action -eq 'Update' -and -not $Online) { throw 'Network access requires -Online. Status is fully offline.' }
if ($Action -eq 'Import' -and -not $PackagePath) { throw 'Import requires a trusted -PackagePath. Verify its official sources first.' }
$exe=Join-Path $PSScriptRoot "bin\$Configuration\net10.0-windows\DesktopCalendar.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build the selected configuration first, using build.ps1. No tools are installed automatically.' }
if ([version](Get-Item -LiteralPath (Join-Path (Split-Path $exe -Parent) 'DesktopCalendar.dll')).VersionInfo.FileVersion -lt [version]'0.7.0') { throw 'This build predates calendar update commands. Rebuild the selected configuration first.' }
$arguments=@('--calendar-action', $Action.ToLowerInvariant())
if ($Online) { $arguments+='--online' }
if ($DataRoot) { $arguments+=@('--data-root', [IO.Path]::GetFullPath($DataRoot)) }
if ($PackagePath) { $arguments+=@('--package', [IO.Path]::GetFullPath($PackagePath)) }
& $exe @arguments | Write-Output
if ($LASTEXITCODE -ne 0) { throw 'Calendar operation failed; previous data retained.' }

[CmdletBinding(SupportsShouldProcess)]
param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$KeepConfiguration='Release', [switch]$Artifacts)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent)).TrimEnd('\')
$tracked=@(& git -C $workspace ls-files)
if($LASTEXITCODE -ne 0){throw 'Cannot check tracked files; cleanup refused.'}
$running=@(foreach($process in Get-Process DesktopCalendar,DesktopHostProbe,PlannerChecks -ErrorAction SilentlyContinue){try {$path=$process.Path}catch {throw 'A running application path is unreadable; cleanup refused.'};if(-not $path){throw 'A running application path is unknown; cleanup refused.'};$path})
$removed=0; $bytes=0L; $skipped=[Collections.Generic.List[string]]::new()
function Remove-Generated([string]$Path) {
    $full=[IO.Path]::GetFullPath($Path)
    if(-not $full.StartsWith($workspace+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup target escaped workspace.'}
    $relative=$full.Substring($workspace.Length+1).Replace('\','/')
    if($relative -notmatch '^(bin/|obj/|Core/bin/|Core/obj/|tests/PlannerChecks/bin/|tests/PlannerChecks/obj/|artifacts/|experiments/|Desktop Calendar P1\.lnk$)'){throw "Unapproved generated target: $relative"}
    if(@($tracked | Where-Object {$_ -eq $relative -or $_.StartsWith($relative+'/',[StringComparison]::OrdinalIgnoreCase)}).Count){throw "Tracked source protected: $relative"}
    if(@($running | Where-Object {$_ -eq $full -or $_.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){$skipped.Add($relative);return}
    if(-not(Test-Path -LiteralPath $full)){return}
    # Verify every parent and descendant without following junctions/symlinks before recursive deletion.
    $cursor=$full
    while($cursor.Length -gt $workspace.Length){if((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Reparse target refused: $relative"};$cursor=Split-Path $cursor -Parent}
    $queue=[Collections.Generic.Stack[string]]::new(); $queue.Push($full); $size=0L
    while($queue.Count){$item=Get-Item -LiteralPath $queue.Pop() -Force;if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Reparse descendant refused: $relative"};if($item.PSIsContainer){foreach($child in Get-ChildItem -LiteralPath $item.FullName -Force){$queue.Push($child.FullName)}}else{$size+=$item.Length}}
    if($PSCmdlet.ShouldProcess($full,'Remove regenerable project output')){Remove-Item -LiteralPath $full -Recurse -Force;$script:removed++;$script:bytes+=$size}
}
foreach($relative in @('bin','obj','Core\bin','Core\obj','tests\PlannerChecks\bin','tests\PlannerChecks\obj')){
    $parent=Join-Path $workspace $relative
    if(Test-Path -LiteralPath $parent){foreach($dir in Get-ChildItem -LiteralPath $parent -Directory -Force | Where-Object Name -ne $KeepConfiguration){Remove-Generated $dir.FullName}}
}
# WPF temporary-project NuGet metadata can otherwise accumulate in obj/ across builds.
$obj=Join-Path $workspace 'obj';if(Test-Path -LiteralPath $obj){foreach($file in Get-ChildItem -LiteralPath $obj -File -Filter '*wpftmp*'){Remove-Generated $file.FullName}}
Remove-Generated (Join-Path $workspace 'Desktop Calendar P1.lnk')
if($Artifacts){
    $root=Join-Path $workspace 'artifacts'
    if(Test-Path -LiteralPath $root){
        $keep=@('package','branding','checks','install-checks.txt','p1-checks.txt')
        foreach($prefix in @('entry-checks-','quality-','preferences-checks-')){$latest=Get-ChildItem -LiteralPath $root -Directory | Where-Object Name -Match ('^'+$prefix+'[a-f0-9]{32}$') | Sort-Object CreationTime -Descending | Select-Object -First 1;if($latest){$keep+=$latest.Name}}
        $directories='^(publish-|entry-checks-|quality-|preferences-checks-)[a-f0-9]{32}$|^(p2-checks|p3-checks|interaction-data-(checks|final|final2)|calendar-(sources|live-update)|final-deployment-smoke|install-checks|lifecycle-checks|real-install-smoke|restart-check-preferences|ui-check-preferences)$'
        $files='^(alpha-check|calendar-\d+-\d+|edge-small-(Left|Right)|month-\d+|probe-(normal|software)-content|render-[\d.]+|settings|timeline-(day|week)-\d+|today-folded|week-\d+)\.png$|^(english|local-host-[nr](-error)?|p1-checks-(normal-[\d-]+|previous-restricted)|p8-testing-section|p9-installed-status|shell-topology(-error)?)\.txt$|^owner-(normal|restricted)-[\d-]+\.json$|^p1\.1-2.*\.(txt|log)$|^Build-CalendarBundle\.ps1$'
        foreach($item in Get-ChildItem -LiteralPath $root -Force){if($keep -notcontains $item.Name -and (($item.PSIsContainer -and $item.Name -match $directories) -or (-not $item.PSIsContainer -and $item.Name -match $files))){Remove-Generated $item.FullName}}
        foreach($category in @('checks\data','checks\wpf','checks\install')){$parent=Join-Path $root $category;if(Test-Path -LiteralPath $parent){$runs=@(Get-ChildItem -LiteralPath $parent -Directory | Where-Object Name -Match '^run-[a-f0-9]{32}$' | Sort-Object CreationTime -Descending);foreach($old in $runs | Select-Object -Skip 1){Remove-Generated $old.FullName}}}
    }
}
Write-Output ("Cleaned {0} targets, {1:N2} MiB. Kept configuration: {2}" -f $removed,($bytes/1MB),$KeepConfiguration)
if($skipped.Count){Write-Output ('RUNNING: kept '+($skipped -join ', ')+'. Exit normally and rerun cleanup.')}

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$package = Join-Path $projectRoot 'artifacts\package'
$testRoot = Join-Path $projectRoot ('artifacts\install-checks\' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($testRoot) | Out-Null
$results = [Collections.Generic.List[string]]::new()
$installer = Join-Path $projectRoot 'Install-DesktopCalendar.ps1'
function Check($name, $condition) { if ($condition) { $results.Add('PASS ' + $name) } else { $results.Add('FAIL ' + $name) } }
function Install($action, $source, $root = $testRoot, $expectFailure = $false) {
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue' # Native stderr is expected for rejection cases in PS5.1.
        & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Action $action -PackagePath $source -TestRoot $root *> (Join-Path $testRoot ('action-' + [Guid]::NewGuid().ToString('N') + '.log'))
        $exit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    if (($exit -ne 0) -ne $expectFailure) { throw "Unexpected installer result: $action, exit=$exit; inspect isolated action logs." }
}
function ClonePackage($name, $change) {
    $path = Join-Path $testRoot $name; [IO.Directory]::CreateDirectory($path) | Out-Null
    foreach ($file in (Get-Content -Encoding UTF8 (Join-Path $package 'package.json') -Raw | ConvertFrom-Json).Files) { Copy-Item -LiteralPath (Join-Path $package $file.Name) -Destination $path }
    Copy-Item -LiteralPath (Join-Path $package 'package.json') -Destination $path
    if ($change) {
        $config = Join-Path $path 'DesktopCalendar.runtimeconfig.json'
        [IO.File]::AppendAllText($config, [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
        $m = Get-Content -Encoding UTF8 (Join-Path $path 'package.json') -Raw | ConvertFrom-Json
        foreach ($file in $m.Files) { $file.Sha256 = (Get-FileHash -LiteralPath (Join-Path $path $file.Name) -Algorithm SHA256).Hash.ToLowerInvariant(); $file.Length = (Get-Item -LiteralPath (Join-Path $path $file.Name)).Length }
        $canonical = ($m.Files | Sort-Object Name | ForEach-Object { $_.Name + ':' + $_.Sha256 }) -join '|'
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $m.PackageId = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
        [IO.File]::WriteAllText((Join-Path $path 'package.json'), ($m | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    }
    return $path
}
function Child($exe, $phase, $directory) {
    $info = [Diagnostics.ProcessStartInfo]::new($exe, '--lifecycle-check=' + $phase)
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.EnvironmentVariables['DESKTOP_CALENDAR_CHECK_ROOT'] = $directory
    return [Diagnostics.Process]::Start($info)
}
$shell = New-Object -ComObject WScript.Shell
$root = Join-Path $testRoot 'Programs\DesktopCalendar'
$link = Join-Path $testRoot 'StartMenu\Desktop Calendar.lnk'
$data = Join-Path $testRoot 'LocalData\DesktopCalendar'
[IO.Directory]::CreateDirectory((Join-Path $data 'assets\images')) | Out-Null
$sentinel = Join-Path $data 'planner-data.json'; [IO.File]::WriteAllText($sentinel, '{"fixtureOnly":true}')
$image = Join-Path $data 'assets\images\keep.txt'; [IO.File]::WriteAllText($image, 'personal-image-fixture')
$initialHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash
try {
    Install 'Install' $package
    $first = Get-Content -Encoding UTF8 (Join-Path $root 'installation.json') -Raw | ConvertFrom-Json
    $firstTarget = $shell.CreateShortcut($link).TargetPath
    Check 'stable user directory target and official icon' ($firstTarget.StartsWith($root + '\') -and $shell.CreateShortcut($link).IconLocation -eq ($firstTarget + ',0') -and (Test-Path -LiteralPath $firstTarget))
    $pe = [IO.BinaryReader]::new([IO.File]::OpenRead($firstTarget))
    try { $pe.BaseStream.Position = 0x3c; $offset = $pe.ReadInt32(); $pe.BaseStream.Position = $offset; Check 'published EXE is Windows x64' ($pe.ReadUInt32() -eq 0x4550 -and $pe.ReadUInt16() -eq 0x8664) } finally { $pe.Dispose() }
    Install 'Install' $package; Install 'Update' $package
    Check 'repeat install/update one shortcut and one version' (@(Get-ChildItem (Split-Path $link -Parent) -Filter '*.lnk').Count -eq 1 -and @(Get-ChildItem (Join-Path $root 'versions') -Directory).Count -eq 1)
    $next = ClonePackage 'package-next' $true
    Install 'Update' $next
    $second = Get-Content -Encoding UTF8 (Join-Path $root 'installation.json') -Raw | ConvertFrom-Json
    $secondTarget = $shell.CreateShortcut($link).TargetPath
    Check 'changed package switches shortcut and retains prior version' ($first.PackageId -ne $second.PackageId -and $firstTarget -ne $secondTarget -and [IO.File]::Exists($firstTarget) -and [IO.File]::Exists($secondTarget))
    $statePath = Join-Path $root 'installation.json'; $heldState = Join-Path $root 'state-held-fixture.json'
    [IO.File]::Move($statePath, $heldState)
    [IO.Directory]::CreateDirectory($statePath) | Out-Null
    try {
        Install 'Install' $package $testRoot $true
        Check 'late metadata failure rolls shortcut back to previous target' ($shell.CreateShortcut($link).TargetPath -eq $secondTarget)
    } finally { [IO.Directory]::Delete($statePath); [IO.File]::Move($heldState, $statePath) }
    $bad = ClonePackage 'package-damaged' $false
    [IO.File]::AppendAllText((Join-Path $bad 'DesktopCalendar.dll'), 'damaged')
    Install 'Update' $bad $testRoot $true
    Check 'corrupt update leaves current entry intact' ($shell.CreateShortcut($link).TargetPath -eq $secondTarget -and (Get-Content -Encoding UTF8 (Join-Path $root 'installation.json') -Raw | ConvertFrom-Json).PackageId -eq $second.PackageId)
    $badManifest = Get-Content -Encoding UTF8 (Join-Path $bad 'package.json') -Raw | ConvertFrom-Json
    $badManifest.Files[0].Name = '..\outside.exe'
    [IO.File]::WriteAllText((Join-Path $bad 'package.json'), ($badManifest | ConvertTo-Json -Depth 6))
    Install 'Update' $bad $testRoot $true
    Check 'manifest path traversal rejected' (-not [IO.File]::Exists((Join-Path $testRoot 'outside.exe')) -and $shell.CreateShortcut($link).TargetPath -eq $secondTarget)
    $holdRoot = Join-Path $testRoot 'hold-check'
    $hold = Child $secondTarget 'hold' $holdRoot
    try {
        for ($i = 0; $i -lt 100 -and -not [IO.File]::Exists((Join-Path $holdRoot 'hold.json')); $i++) { Start-Sleep -Milliseconds 100 }
        if (-not [IO.File]::Exists((Join-Path $holdRoot 'hold.json'))) { throw 'Installed hold child not ready.' }
        Install 'Uninstall' $package $testRoot $true
        Check 'running installed program protects uninstall' ([IO.File]::Exists($secondTarget) -and [IO.File]::Exists($link))
    } finally { if (-not $hold.HasExited) { $hold.Kill(); $hold.WaitForExit() }; $hold.Dispose() }
    $smokeRoot = Join-Path $testRoot 'smoke-check'
    $smoke = Child $secondTarget 'installed' $smokeRoot
    try {
        if (-not $smoke.WaitForExit(20000)) { $smoke.Kill(); throw 'Installed smoke timed out.' }
        $report = Get-Content -Encoding UTF8 (Join-Path $smokeRoot 'installed.json') -Raw | ConvertFrom-Json
        Check 'installed EXE actually loads branded WPF desktop component and exits' ($smoke.ExitCode -eq 0 -and $report.Ready -and $report.IsVisible -and $report.Brand -and $report.DesktopOwner -and $report.Executable -eq $secondTarget)
    } finally { $smoke.Dispose() }
    $foreign = Join-Path $testRoot 'foreign-shortcut'; [IO.Directory]::CreateDirectory((Join-Path $foreign 'StartMenu')) | Out-Null
    $foreignLink = Join-Path $foreign 'StartMenu\Desktop Calendar.lnk'; $other = $shell.CreateShortcut($foreignLink)
    $other.TargetPath = Join-Path $env:WINDIR 'notepad.exe'; $other.Description = 'Foreign fixture'; $other.Save()
    Install 'Install' $package $foreign $true
    Check 'foreign same-name shortcut is never overwritten' ($shell.CreateShortcut($foreignLink).Description -eq 'Foreign fixture')
    $foreignFolder = Join-Path $testRoot 'foreign-folder'
    $foreignProgram = Join-Path $foreignFolder 'Programs\DesktopCalendar'
    [IO.Directory]::CreateDirectory($foreignProgram) | Out-Null
    [IO.File]::WriteAllText((Join-Path $foreignProgram 'keep.txt'), 'unmanaged fixture')
    Install 'Install' $package $foreignFolder $true
    Check 'unmanaged existing program directory is not adopted' ([IO.File]::Exists((Join-Path $foreignProgram 'keep.txt')) -and -not [IO.File]::Exists((Join-Path $foreignProgram '.desktop-calendar-owned')))
    Install 'Uninstall' $package $projectRoot $true
    Check 'test root escaping artifacts is rejected' ([IO.File]::Exists((Join-Path $projectRoot 'AGENTS.md')))
    [IO.File]::WriteAllText((Join-Path $root 'keep-foreign.txt'), 'foreign fixture')
    Install 'Uninstall' $package; Install 'Uninstall' $package
    Check 'uninstall removes only managed entry and program versions' (-not [IO.File]::Exists($link) -and -not [IO.File]::Exists($firstTarget) -and -not [IO.File]::Exists($secondTarget))
    Check 'uninstall preserves personal data/images and foreign files' ((Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -eq $initialHash -and [IO.File]::Exists($image) -and [IO.File]::Exists((Join-Path $root 'keep-foreign.txt')))
} catch { $results.Add('FAIL unhandled install check: ' + $_.Exception.Message) }
$reportPath = Join-Path $projectRoot 'artifacts\install-checks.txt'
[IO.File]::WriteAllLines($reportPath, $results, [Text.UTF8Encoding]::new($false))
$failed = @($results | Where-Object { $_ -like 'FAIL *' }).Count
Write-Output ('Install checks PASS=' + @($results | Where-Object { $_ -like 'PASS *' }).Count + ' FAIL=' + $failed)
$results | Where-Object { $_ -like 'FAIL *' }
if ($failed -gt 0) { throw "Install checks failed: $testRoot" }

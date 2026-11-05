param(
    [ValidateSet('Install', 'Update', 'Uninstall', 'Status')][string]$Action = 'Install',
    [string]$PackagePath,
    # Tests only: an isolated path strictly beneath this repository's artifacts.
    [string]$TestRoot
)
$ErrorActionPreference = 'Stop'
$appId = 'desktop-calendar'
$description = 'Desktop Calendar (current user)'
function Full($path) { [IO.Path]::GetFullPath($path).TrimEnd('\') }
function Inside($path, $parent) { (Full $path).StartsWith((Full $parent) + '\', [StringComparison]::OrdinalIgnoreCase) }
if ($TestRoot) {
    $TestRoot = Full $TestRoot
    if (-not (Inside $TestRoot (Join-Path $PSScriptRoot 'artifacts'))) { throw 'TestRoot must be beneath repository artifacts.' }
    $root = Join-Path $TestRoot 'Programs\DesktopCalendar'; $programs = Join-Path $TestRoot 'StartMenu'
} else {
    $root = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\DesktopCalendar'
    $programs = [Environment]::GetFolderPath('Programs')
}
$root = Full $root
$link = Join-Path $programs 'Desktop Calendar.lnk'
$startup = if ($TestRoot) { Join-Path $TestRoot 'Startup' } else { [Environment]::GetFolderPath('Startup') }
$startupLink = Join-Path $startup 'Desktop Calendar.lnk'
function OwnedStartup {
    if (-not [IO.File]::Exists($startupLink)) { return $false }
    $item = $shell.CreateShortcut($startupLink)
    return $item.Description -eq 'Desktop Calendar startup (current user)' -and $item.Arguments -eq '--autostart' -and (Inside $item.TargetPath $root)
}
$marker = Join-Path $root '.desktop-calendar-owned'
$statePath = Join-Path $root 'installation.json'
function NoReparse($path) {
    if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Refusing reparse point: $path" }
}
function NoReparseChain($path) {
    $part = Full $path
    while ($part) { NoReparse $part; $next = Split-Path $part -Parent; if ($next -eq $part) { break }; $part = $next }
}
function ManagedPath($relative) {
    $path = Full (Join-Path $root $relative)
    if (-not (Inside $path $root)) { throw 'Path escapes install root.' }
    $part = $path
    while ((Inside $part $root) -or $part -eq $root) { NoReparse $part; if ($part -eq $root) { break }; $part = Split-Path $part -Parent }
    return $path
}
function AtomicBytes($path, [byte[]]$bytes) {
    $temporary = $path + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $stream = [IO.FileStream]::new($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None, 4096, [IO.FileOptions]::WriteThrough)
        try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        if ([IO.File]::Exists($path)) { [IO.File]::Replace($temporary, $path, [System.Management.Automation.Language.NullString]::Value) } else { [IO.File]::Move($temporary, $path) }
    } finally { if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) } }
}
function JsonBytes($value) { return ,([Text.Encoding]::UTF8.GetBytes(($value | ConvertTo-Json -Depth 8))) }
function ReadPackage($path) {
    NoReparse $path
    if ((Get-Item -LiteralPath $path).Length -gt 65536) { throw 'Package manifest too large.' }
    $m = [IO.File]::ReadAllText($path) | ConvertFrom-Json
    $required = @('DesktopCalendar.exe', 'DesktopCalendar.dll', 'DesktopCalendar.deps.json', 'DesktopCalendar.runtimeconfig.json')
    $allowed = $required + @('DesktopCalendar.Core.dll', 'lunar.dll', 'THIRD-PARTY-NOTICES.txt') # Retain old package support.
    if ($m.SchemaVersion -ne 1 -or $m.AppId -ne $appId -or $m.Architecture -ne 'x64' -or $m.SelfContained -ne $false -or $m.PackageId -notmatch '^[a-f0-9]{64}$' -or @($m.Files).Count -notin @(4,5,7)) { throw 'Invalid package manifest.' }
    if (@($m.Files.Name | Sort-Object -Unique).Count -ne @($m.Files).Count -or @($required | Where-Object { $_ -notin $m.Files.Name }).Count -ne 0) { throw 'Duplicate or missing package paths.' }
    $packageVersion = $null
    if (-not [Version]::TryParse([string]$m.Version, [ref]$packageVersion)) { throw 'Invalid program version.' }
    if ($packageVersion -ge [Version]'0.3.0' -and 'DesktopCalendar.Core.dll' -notin $m.Files.Name) { throw 'Required planner Core dependency missing.' }
    if ($packageVersion -ge [Version]'0.8.4' -and @(@('lunar.dll', 'THIRD-PARTY-NOTICES.txt') | Where-Object { $_ -notin $m.Files.Name }).Count) { throw 'Required almanac dependency or license missing.' }
    foreach ($file in $m.Files) {
        if ($file.Name -notin $allowed -or $file.Sha256 -notmatch '^[a-f0-9]{64}$' -or $file.Length -le 0) { throw 'Invalid package entry.' }
    }
    $canonical = ($m.Files | Sort-Object Name | ForEach-Object { $_.Name + ':' + $_.Sha256 }) -join '|'
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $id = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
    if ($id -ne $m.PackageId) { throw 'Package ID mismatch.' }
    return $m
}
function VerifyFiles($directory, $manifest) {
    foreach ($file in $manifest.Files) {
        $path = Join-Path $directory $file.Name; NoReparse $path
        if (-not [IO.File]::Exists($path) -or (Get-Item -LiteralPath $path).Length -ne $file.Length -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) { throw "Package file damaged: $($file.Name)" }
    }
}
$shell = New-Object -ComObject WScript.Shell
function OwnedLink {
    if (-not [IO.File]::Exists($link)) { return $false }
    $shortcut = $shell.CreateShortcut($link)
    return $shortcut.Description -eq $description -and (Inside $shortcut.TargetPath $root)
}
$mutex = [Threading.Mutex]::new($false, 'Local\DesktopCalendar-UserInstall')
$locked = $false
try {
    try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (-not $locked) { throw 'Another Desktop Calendar installer is running.' }
    NoReparseChain $root; NoReparseChain $programs; NoReparseChain $startup; NoReparse $startupLink; NoReparse $link; NoReparse $marker; NoReparse $statePath
    if (Test-Path -LiteralPath $root) {
        if ([IO.File]::Exists($marker)) { if ([IO.File]::ReadAllText($marker) -ne $appId) { throw 'Install marker mismatch.' } }
        elseif (@(Get-ChildItem -LiteralPath $root -Force).Count -gt 0) { throw 'Existing folder is not owned by this installer.' }
    }
    if ($Action -eq 'Status') {
        if ([IO.File]::Exists($statePath)) { [IO.File]::ReadAllText($statePath) } else { Write-Output 'Not installed.' }
        return
    }
    if ($Action -eq 'Uninstall') {
        if (-not [IO.File]::Exists($marker)) { Write-Output 'Not installed; no files changed.'; return }
        foreach ($process in @(Get-Process DesktopCalendar -ErrorAction SilentlyContinue)) {
            try { $runningPath = $process.Path } catch { throw 'Cannot inspect running app. Exit it first.' }
            if ($runningPath -and (Inside $runningPath $root)) { throw 'Exit the installed app from its tray/settings before uninstalling.' }
        }
        if ([IO.File]::Exists($link)) {
            if (-not (OwnedLink)) { throw 'The existing Start Menu shortcut is not managed by this installer.' }
            [IO.File]::Delete($link)
        }
        if (OwnedStartup) { [IO.File]::Delete($startupLink) }
        $versions = ManagedPath 'versions'
        if ([IO.Directory]::Exists($versions)) {
            foreach ($version in Get-ChildItem -LiteralPath $versions -Directory) {
                if ($version.Name -notmatch '^v[a-f0-9]{16}$') { continue }
                $directory = ManagedPath ('versions\' + $version.Name)
                $packageFile = Join-Path $directory 'package.json'; NoReparse $packageFile
                if (-not [IO.File]::Exists($packageFile)) { continue }
                $m = ReadPackage $packageFile
                if ($version.Name -ne ('v' + $m.PackageId.Substring(0,16))) { throw 'Version marker mismatch.' }
                foreach ($file in $m.Files) {
                    $path = ManagedPath ('versions\' + $version.Name + '\' + $file.Name)
                    if ([IO.File]::Exists($path) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $file.Sha256) { [IO.File]::Delete($path) }
                }
                if (@(Get-ChildItem -LiteralPath $directory -Force).Count -eq 1) { [IO.File]::Delete($packageFile); [IO.Directory]::Delete($directory) }
            }
            if (@(Get-ChildItem -LiteralPath $versions -Force).Count -eq 0) { [IO.Directory]::Delete($versions) }
        }
        if ([IO.File]::Exists($statePath)) { [IO.File]::Delete($statePath) }
        if (@(Get-ChildItem -LiteralPath $root -Force).Count -eq 1) { [IO.File]::Delete($marker); [IO.Directory]::Delete($root) }
        Write-Output 'Entry removed; only unchanged managed program files removed. User data was not accessed. Any foreign/modified files remain.'
        return
    }
    if ($Action -eq 'Update' -and -not [IO.File]::Exists($statePath)) { throw 'Install first, then update.' }
    if (-not $PackagePath) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Publish-DesktopCalendar.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
        $PackagePath = Join-Path $PSScriptRoot 'artifacts\package'
    }
    $PackagePath = Full $PackagePath; NoReparseChain $PackagePath
    $manifest = ReadPackage (Join-Path $PackagePath 'package.json'); VerifyFiles $PackagePath $manifest
    $runtimes = & (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe') --list-runtimes
    if ($LASTEXITCODE -ne 0 -or -not ($runtimes -match '^Microsoft.WindowsDesktop.App 10\.')) { throw 'Install the official .NET Desktop Runtime 10 x64 first. No software was installed by this script.' }
    if ([IO.File]::Exists($link) -and -not (OwnedLink)) { throw 'A different Start Menu entry already uses Desktop Calendar. Nothing was replaced.' }
    [IO.Directory]::CreateDirectory($root) | Out-Null
    if (-not [IO.File]::Exists($marker)) { AtomicBytes $marker ([Text.Encoding]::UTF8.GetBytes($appId)) }
    $versionName = 'v' + $manifest.PackageId.Substring(0,16)
    $versionPath = ManagedPath ('versions\' + $versionName)
    if (-not [IO.Directory]::Exists($versionPath)) {
        $staging = ManagedPath ('staging-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($staging) | Out-Null
        foreach ($file in $manifest.Files) { [IO.File]::Copy((Join-Path $PackagePath $file.Name), (Join-Path $staging $file.Name)) }
        [IO.File]::Copy((Join-Path $PackagePath 'package.json'), (Join-Path $staging 'package.json'))
        VerifyFiles $staging $manifest
        [IO.Directory]::CreateDirectory((Split-Path $versionPath -Parent)) | Out-Null
        [IO.Directory]::Move($staging, $versionPath)
    } else {
        $existing = ReadPackage (Join-Path $versionPath 'package.json')
        if ($existing.PackageId -ne $manifest.PackageId) { throw 'Version collision.' }
        VerifyFiles $versionPath $manifest
    }
    [IO.Directory]::CreateDirectory($programs) | Out-Null
    $exe = Join-Path $versionPath 'DesktopCalendar.exe'
    $temporaryLink = ManagedPath ('launcher-' + [Guid]::NewGuid().ToString('N') + '.lnk')
    $oldLink = if ([IO.File]::Exists($link)) { [IO.File]::ReadAllBytes($link) } else { $null }
    $oldStartup = if (OwnedStartup) { [IO.File]::ReadAllBytes($startupLink) } else { $null }
    try {
        $shortcut = $shell.CreateShortcut($temporaryLink); $shortcut.TargetPath = $exe; $shortcut.IconLocation = $exe + ',0'
        $shortcut.WorkingDirectory = $versionPath; $shortcut.Description = $description; $shortcut.WindowStyle = 1; $shortcut.Save()
        $linkBytes = [IO.File]::ReadAllBytes($temporaryLink)
        AtomicBytes $link $linkBytes
        try {
            if ($null -ne $oldStartup) {
                $auto = $shell.CreateShortcut($temporaryLink); $auto.TargetPath = $exe; $auto.Arguments = '--autostart'; $auto.WorkingDirectory = $versionPath; $auto.IconLocation = $exe + ',0'; $auto.Description = 'Desktop Calendar startup (current user)'; $auto.Save()
                AtomicBytes $startupLink ([IO.File]::ReadAllBytes($temporaryLink))
            }
            AtomicBytes $statePath (JsonBytes ([ordered]@{ SchemaVersion = 1; AppId = $appId; Version = $manifest.Version; PackageId = $manifest.PackageId; Current = 'versions\' + $versionName }))
        }
        catch { if ($null -ne $oldLink) { AtomicBytes $link $oldLink } else { [IO.File]::Delete($link) }; if ($null -ne $oldStartup) { AtomicBytes $startupLink $oldStartup }; throw }
    } finally { if ([IO.File]::Exists($temporaryLink)) { [IO.File]::Delete($temporaryLink) } }
    Write-Output "Installed: $exe"
    Write-Output "Start Menu: $link"
    Write-Output 'No new auto-start entry, desktop shortcut, PATH, registry or data-directory changes. Existing opted-in startup follows updates. Exit older running copies before starting this version.'
} finally { if ($locked) { $mutex.ReleaseMutex() }; $mutex.Dispose() }

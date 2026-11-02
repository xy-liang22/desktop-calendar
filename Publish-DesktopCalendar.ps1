param([ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')][string]$Configuration = 'P117')
$ErrorActionPreference = 'Stop'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'DesktopCalendar-DotnetCli'
$staging = Join-Path $PSScriptRoot ('artifacts\publish-' + [Guid]::NewGuid().ToString('N'))
& (Join-Path $env:ProgramFiles 'dotnet\dotnet.exe') publish (Join-Path $PSScriptRoot 'DesktopCalendar.csproj') --no-restore -c $Configuration --self-contained false -o $staging
if ($LASTEXITCODE -ne 0) { throw 'Offline framework-dependent publish failed.' }
$package = Join-Path $PSScriptRoot 'artifacts\package'
[IO.Directory]::CreateDirectory($package) | Out-Null
$files = @()
foreach ($name in @('DesktopCalendar.exe', 'DesktopCalendar.dll', 'DesktopCalendar.Core.dll', 'DesktopCalendar.deps.json', 'DesktopCalendar.runtimeconfig.json')) {
    Copy-Item -LiteralPath (Join-Path $staging $name) -Destination (Join-Path $package $name) -Force
    $item = Get-Item -LiteralPath (Join-Path $package $name)
    $files += [pscustomobject][ordered]@{ Name = $name; Length = $item.Length; Sha256 = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$canonical = ($files | Sort-Object Name | ForEach-Object { $_.Name + ':' + $_.Sha256 }) -join '|'
$sha = [Security.Cryptography.SHA256]::Create()
try { $id = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)))).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
$version = ([xml][IO.File]::ReadAllText((Join-Path $PSScriptRoot 'DesktopCalendar.csproj'))).Project.PropertyGroup.Version
$manifest = [ordered]@{ SchemaVersion = 1; AppId = 'desktop-calendar'; Version = $version; PackageId = $id; Architecture = 'x64'; SelfContained = $false; Files = $files }
[IO.File]::WriteAllText((Join-Path $package 'package.json'), ($manifest | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Output "Package: $package (framework-dependent, Windows Desktop Runtime 10 x64 required)"

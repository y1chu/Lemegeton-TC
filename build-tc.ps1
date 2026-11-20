[CmdletBinding()]
param(
    [ValidateSet('Release (Mini)', 'Release (Full)', 'Debug (Mini)', 'Debug (Full)')]
    [string] $Configuration = 'Release (Mini)',
    [string] $DalamudLibPath = (Join-Path $env:APPDATA 'FFXIVSimpleLauncher/Dalamud/Injector'),
    [string] $DotnetPath,
    [string] $GamePath
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

if (-not $DotnetPath) {
    $localSdk = Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $localSdk) {
        $DotnetPath = $localSdk
    } else {
        $DotnetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    }
}

$hostDll = Join-Path $DalamudLibPath 'Dalamud.dll'
if (-not (Test-Path -LiteralPath $hostDll)) {
    throw "Taiwan Dalamud.dll not found: $hostDll"
}
$hostVersion = [Reflection.AssemblyName]::GetAssemblyName($hostDll).Version
if ($hostVersion.Major -ne 13) {
    throw "Taiwan API 13 is required; selected host is $hostVersion."
}

if (-not $GamePath) {
    $settingsPath = Join-Path $env:APPDATA 'FFXIVSimpleLauncher/settings.json'
    if (Test-Path -LiteralPath $settingsPath) {
        $GamePath = (Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).GamePath
    }
}
$gameVersion = $null
if ($GamePath) {
    $gameVersion = (Get-Content -LiteralPath (Join-Path $GamePath 'game/ffxivgame.ver') -Raw).Trim()
    $manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'Lemegeton/Lemegeton.json') -Raw | ConvertFrom-Json
    if ($gameVersion -ne $manifest.ApplicableVersion) {
        throw "Installed client is $gameVersion; this package targets $($manifest.ApplicableVersion). Update and verify the TC opcode profile before building for that client."
    }
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
Push-Location -LiteralPath $repoRoot
try {
    & $DotnetPath build Lemegeton/Lemegeton.csproj --configuration $Configuration "-p:DalamudLibPath=$DalamudLibPath" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

    $variant = if ($Configuration -like '*(Mini)') { 'mini' } else { 'full' }
    $outputDir = Join-Path $repoRoot "Lemegeton/bin/$Configuration"
    $packageDir = Join-Path $repoRoot "artifacts/tc-$variant"
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    foreach ($fileName in @('Lemegeton.dll', 'Lemegeton.json')) {
        Copy-Item -LiteralPath (Join-Path $outputDir $fileName) -Destination (Join-Path $packageDir $fileName) -Force
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $packageDir 'LICENSE') -Force
    $packageFiles = @('Lemegeton.dll', 'Lemegeton.json', 'LICENSE') | ForEach-Object { Join-Path $packageDir $_ }
    $zipPath = Join-Path $repoRoot "artifacts/Lemegeton-TC-$variant.zip"
    Compress-Archive -LiteralPath $packageFiles -DestinationPath $zipPath -Force
    Write-Host "TC host: $hostVersion; client: $gameVersion"
    Write-Host "Development plugin: $(Join-Path $packageDir 'Lemegeton.dll')"
    Write-Host "Package: $zipPath"
} finally {
    Pop-Location
}

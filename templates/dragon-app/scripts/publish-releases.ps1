[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Version,
    [ValidateSet('All', 'Desktop', 'Browser', 'Android')]
    [string[]]$Platform = @('Desktop', 'Browser', 'Android'),
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')]
    [string[]]$DesktopRuntime = @('win-x64'),
    [bool]$SelfContainedDesktop = $true,
    [int]$AndroidVersionCode = 0,
    [ValidateSet('Both', 'apk', 'aab')]
    [string]$AndroidPackageFormat = 'Both',
    [ValidateSet('Unsigned', 'Auto', 'Keystore')]
    [string]$AndroidSigningMode = 'Unsigned',
    [switch]$SkipTests,
    [switch]$RunScreenshotQa,
    [switch]$RunCleanSlateQa,
    [switch]$RunPerformanceQa,
    [string]$ReleaseRoot
)

. (Join-Path $PSScriptRoot '_dragon-appkit.ps1')
$ErrorActionPreference = 'Stop'
$config = Get-DragonAppConfig -RepoRoot $repoRoot
$release = Get-DragonReleaseVersion -Version $Version -Config $config
$root = if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    Join-Path $repoRoot (Join-Path 'artifacts\releases' $release.Tag)
} else {
    $ReleaseRoot
}

New-Item -ItemType Directory -Path $root -Force | Out-Null
$solution = Join-Path $repoRoot 'DragonTemplate.slnx'

dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

dotnet build $solution --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$testsRun = $false
if (-not $SkipTests) {
    dotnet test $solution --configuration $Configuration --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $testsRun = $true
}

$selected = if ($Platform -contains 'All') { @('Desktop', 'Browser', 'Android') } else { $Platform }

if ($selected -contains 'Desktop') {
    foreach ($runtime in $DesktopRuntime) {
        $out = Join-Path $root "Desktop-$runtime"
        dotnet publish (Join-Path $repoRoot 'DragonTemplate.Desktop\DragonTemplate.Desktop.csproj') --configuration $Configuration --runtime $runtime --self-contained $SelfContainedDesktop --output $out
        if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed for $runtime." }
        Compress-Archive -Path (Join-Path $out '*') -DestinationPath (Join-Path $root "DragonTemplate-Desktop-$runtime-$($release.Tag).zip") -Force
    }
}

if ($selected -contains 'Browser') {
    $out = Join-Path $root 'Browser'
    dotnet publish (Join-Path $repoRoot 'DragonTemplate.Browser\DragonTemplate.Browser.csproj') --configuration $Configuration --output $out
    if ($LASTEXITCODE -ne 0) { throw 'Browser publish failed.' }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath (Join-Path $root "DragonTemplate-Browser-$($release.Tag).zip") -Force
}

if ($selected -contains 'Android') {
    $out = Join-Path $root 'Android'
    $androidArgs = @(
        'publish',
        (Join-Path $repoRoot 'DragonTemplate.Android\DragonTemplate.Android.csproj'),
        '--configuration',
        $Configuration,
        '--output',
        $out
    )

    if ($AndroidSigningMode -eq 'Unsigned') {
        $androidArgs += '-p:AndroidKeyStore=false'
    }

    if ($AndroidVersionCode -gt 0) {
        $androidArgs += "-p:ApplicationVersion=$AndroidVersionCode"
    }

    if ($AndroidPackageFormat -ne 'Both') {
        $androidArgs += "-p:AndroidPackageFormat=$AndroidPackageFormat"
    }

    dotnet @androidArgs
    if ($LASTEXITCODE -ne 0) { throw 'Android publish failed.' }
}

if ($RunScreenshotQa) {
    & (Join-Path $PSScriptRoot 'screenshot-qa.ps1') -Version $release.Version
}

if ($RunCleanSlateQa) {
    & (Join-Path $PSScriptRoot 'clean-slate-qa.ps1') -Version $release.Version
}

if ($RunPerformanceQa) {
    & (Join-Path $PSScriptRoot 'performance-qa.ps1') -Version $release.Version
}

New-DragonReleaseManifest -RepoRoot $repoRoot -ReleaseRoot $root -Version $release.Version -Platforms $selected -TestsRun:$testsRun

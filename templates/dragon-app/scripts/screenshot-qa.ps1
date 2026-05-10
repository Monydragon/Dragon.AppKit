[CmdletBinding()]
param(
    [string]$Version = '0.1.0',
    [string]$OutputRoot,
    [switch]$PlanOnly
)

. (Join-Path $PSScriptRoot '_dragon-appkit.ps1')
$config = Get-DragonAppConfig -RepoRoot $repoRoot
$release = Get-DragonReleaseVersion -Version $Version -Config $config
$root = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repoRoot (Join-Path 'artifacts\qa\screenshots' $release.Tag)
} else {
    $OutputRoot
}

New-Item -ItemType Directory -Path $root -Force | Out-Null
$manifestPath = Join-Path $root 'screenshot-manifest.csv'
$rows = @(
    'viewport,screen,width,height,status',
    'desktop-landscape,Home,1440,960,Planned',
    'desktop-portrait,Home,960,1200,Planned',
    'tablet-narrow,Home,768,1024,Planned',
    'phone,Home,390,844,Planned'
)
Set-Content -Path $manifestPath -Value ($rows -join [Environment]::NewLine) -Encoding utf8
Write-Host "Screenshot QA manifest: $manifestPath"


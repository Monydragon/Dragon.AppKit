[CmdletBinding()]
param(
    [string]$Version = '0.1.0',
    [string]$OutputRoot
)

. (Join-Path $PSScriptRoot '_dragon-appkit.ps1')
$config = Get-DragonAppConfig -RepoRoot $repoRoot
$release = Get-DragonReleaseVersion -Version $Version -Config $config
$root = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repoRoot (Join-Path 'artifacts\qa\performance' $release.Tag)
} else {
    $OutputRoot
}

New-Item -ItemType Directory -Path $root -Force | Out-Null
$path = Join-Path $root 'performance-qa.json'
[ordered]@{
    productName = $config.productName
    version = $release.Version
    status = 'Planned'
    budgets = @('startup', 'navigation', 'large-local-data-set')
    generatedAt = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json -Depth 4 | Set-Content -Path $path -Encoding utf8
Write-Host "Performance QA plan: $path"


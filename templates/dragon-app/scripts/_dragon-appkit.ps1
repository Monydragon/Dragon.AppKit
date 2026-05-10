$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$appKitRoot = if (-not [string]::IsNullOrWhiteSpace($env:DRAGON_APPKIT_ROOT)) {
    $env:DRAGON_APPKIT_ROOT
} else {
    Join-Path (Split-Path -Parent $repoRoot) 'Dragon.AppKit'
}

$modulePath = Join-Path $appKitRoot 'build\DragonRelease\DragonRelease.psm1'
if (-not (Test-Path $modulePath)) {
    throw "Dragon.AppKit module was not found. Set DRAGON_APPKIT_ROOT. Expected: $modulePath"
}

Import-Module $modulePath -Force


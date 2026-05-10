[CmdletBinding()]
param(
    [string]$Version,
    [string]$ReleaseRoot,
    [ValidateSet('Unsigned', 'Signed', 'Mixed')]
    [string]$SigningStatus = 'Unsigned',
    [switch]$RunScreenshotQa,
    [switch]$RunCleanSlateQa,
    [switch]$RunPerformanceQa,
    [switch]$RequireSignedArtifacts,
    [switch]$PlanOnly
)

. (Join-Path $PSScriptRoot '_dragon-appkit.ps1')
New-DragonReleaseCandidateGate `
    -RepoRoot $repoRoot `
    -Version $Version `
    -ReleaseRoot $ReleaseRoot `
    -SigningStatus $SigningStatus `
    -RunScreenshotQa:$RunScreenshotQa `
    -RunCleanSlateQa:$RunCleanSlateQa `
    -RunPerformanceQa:$RunPerformanceQa `
    -RequireSignedArtifacts:$RequireSignedArtifacts `
    -PlanOnly:$PlanOnly


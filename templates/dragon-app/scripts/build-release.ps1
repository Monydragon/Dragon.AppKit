[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Version,
    [ValidateSet('All', 'Desktop', 'Browser', 'Android')]
    [string[]]$Platform = @('Desktop', 'Browser', 'Android'),
    [switch]$SkipTests
)

& (Join-Path $PSScriptRoot 'publish-releases.ps1') -Configuration $Configuration -Version $Version -Platform $Platform -SkipTests:$SkipTests


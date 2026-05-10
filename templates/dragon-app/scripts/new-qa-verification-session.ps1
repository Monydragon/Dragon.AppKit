[CmdletBinding()]
param(
    [string]$TesterName = 'QA',
    [string]$BuildLabel,
    [string]$Platform = 'Windows Desktop',
    [string]$Viewport = '1440x900'
)

. (Join-Path $PSScriptRoot '_dragon-appkit.ps1')
New-DragonQaSession -RepoRoot $repoRoot -TesterName $TesterName -BuildLabel $BuildLabel -Platform $Platform -Viewport $Viewport


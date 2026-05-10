[CmdletBinding()]
param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
dotnet run --project (Join-Path $repoRoot 'Dragon.AppKit.Publisher\Dragon.AppKit.Publisher.csproj') --configuration $Configuration


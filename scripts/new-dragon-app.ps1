[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Name,

    [Parameter(Mandatory)]
    [string]$Product,

    [Parameter(Mandatory)]
    [string]$AppId,

    [Parameter(Mandatory)]
    [string]$EnvPrefix,

    [string]$Targets = 'Desktop,Android,Browser',

    [string]$OutputDirectory = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$templatePath = Join-Path $repoRoot 'templates\dragon-app'

dotnet new install $templatePath --force | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Template install failed with exit code $LASTEXITCODE."
}

dotnet new dragon-app `
    -n $Name `
    -o (Join-Path $OutputDirectory $Name) `
    --product $Product `
    --app-id $AppId `
    --env-prefix $EnvPrefix `
    --targets $Targets

if ($LASTEXITCODE -ne 0) {
    throw "Dragon app creation failed with exit code $LASTEXITCODE."
}

Write-Host "Created $Product at $(Join-Path $OutputDirectory $Name)"

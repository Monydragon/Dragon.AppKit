[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$templatePath = Join-Path $repoRoot 'templates\dragon-app'

dotnet new install $templatePath --force
if ($LASTEXITCODE -ne 0) {
    throw "Template install failed with exit code $LASTEXITCODE."
}

Write-Host "Installed Dragon app template from $templatePath"

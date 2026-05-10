[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $env:TEMP 'DragonAppKitTemplateTest')
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$templatePath = Join-Path $repoRoot 'templates\dragon-app'
$sampleRoot = Join-Path $OutputDirectory ('DragonSample-' + [Guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $sampleRoot -Force | Out-Null
dotnet new install $templatePath --force | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Template install failed with exit code $LASTEXITCODE."
}

dotnet new dragon-app `
    -n DragonSample `
    -o $sampleRoot `
    --product 'Dragon Sample' `
    --app-id com.dragonsample.app `
    --env-prefix DRAGON_SAMPLE `
    --targets Desktop,Android,Browser | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Template creation failed with exit code $LASTEXITCODE."
}

dotnet restore (Join-Path $sampleRoot 'DragonSample.slnx') | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Template restore failed with exit code $LASTEXITCODE."
}

dotnet test (Join-Path $sampleRoot 'DragonSample.slnx') --no-restore | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Template tests failed with exit code $LASTEXITCODE."
}

Write-Host "Template validation passed: $sampleRoot"

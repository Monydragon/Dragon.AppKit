[CmdletBinding()]
param(
    [int]$Seconds = 5,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'DragonTemplate.Desktop\DragonTemplate.Desktop.csproj'

if (-not $NoBuild) {
    dotnet build $project
    if ($LASTEXITCODE -ne 0) {
        throw "Desktop build failed with exit code $LASTEXITCODE."
    }
}

$args = @('run', '--project', $project)
if ($NoBuild) {
    $args += '--no-build'
}

$process = Start-Process -FilePath 'dotnet' -ArgumentList $args -PassThru -WindowStyle Hidden
Start-Sleep -Seconds $Seconds
if ($process.HasExited) {
    throw "Desktop smoke test failed; process exited with code $($process.ExitCode)."
}

Stop-Process -Id $process.Id -Force
Write-Host 'Desktop smoke test passed.'


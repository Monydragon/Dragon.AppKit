Set-StrictMode -Version 2.0

function Find-DragonAppKitRoot {
    [CmdletBinding()]
    param(
        [string]$StartDirectory = (Get-Location).Path
    )

    if (-not [string]::IsNullOrWhiteSpace($env:DRAGON_APPKIT_ROOT) -and (Test-Path $env:DRAGON_APPKIT_ROOT)) {
        return (Resolve-Path $env:DRAGON_APPKIT_ROOT).Path
    }

    $directory = (Resolve-Path $StartDirectory).Path
    while ($true) {
        $candidate = Join-Path $directory 'Dragon.AppKit'
        if (Test-Path (Join-Path $candidate 'build\DragonRelease\DragonRelease.psm1')) {
            return (Resolve-Path $candidate).Path
        }

        $sibling = Join-Path (Split-Path -Parent $directory) 'Dragon.AppKit'
        if (Test-Path (Join-Path $sibling 'build\DragonRelease\DragonRelease.psm1')) {
            return (Resolve-Path $sibling).Path
        }

        $parent = Split-Path -Parent $directory
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $directory) {
            break
        }

        $directory = $parent
    }

    throw 'Dragon.AppKit was not found. Set DRAGON_APPKIT_ROOT or place Dragon.AppKit beside the product repo.'
}

function Get-DragonAppConfig {
    [CmdletBinding()]
    param(
        [string]$RepoRoot = (Get-Location).Path
    )

    $configPath = Join-Path $RepoRoot 'dragon-app.json'
    if (-not (Test-Path $configPath)) {
        throw "dragon-app.json was not found at $configPath"
    }

    $config = Get-Content -Raw -Path $configPath | ConvertFrom-Json
    $config | Add-Member -NotePropertyName RepoRoot -NotePropertyValue (Resolve-Path $RepoRoot).Path -Force
    $config | Add-Member -NotePropertyName ConfigPath -NotePropertyValue (Resolve-Path $configPath).Path -Force
    return $config
}

function Test-DragonAppContract {
    [CmdletBinding()]
    param(
        [string]$RepoRoot = (Get-Location).Path
    )

    $config = Get-DragonAppConfig -RepoRoot $RepoRoot
    $required = @('productName', 'shortName', 'namespace', 'appId', 'envPrefix', 'version', 'androidVersionCode', 'supportedTargets')
    $missing = @()

    foreach ($name in $required) {
        if (-not ($config.PSObject.Properties.Name -contains $name) -or [string]::IsNullOrWhiteSpace([string]$config.$name)) {
            $missing += $name
        }
    }

    if ($missing.Count -gt 0) {
        throw "dragon-app.json is missing required field(s): $($missing -join ', ')"
    }

    if ([string]$config.envPrefix -cnotmatch '^[A-Z][A-Z0-9_]+$') {
        throw "envPrefix must be uppercase snake case. Actual: $($config.envPrefix)"
    }

    if (-not ($config.supportedTargets -contains 'Desktop')) {
        Write-Warning 'Desktop is not listed in supportedTargets; most Dragon baseline workflows expect a desktop host.'
    }

    return [pscustomobject]@{
        ProductName = $config.productName
        Version = $config.version
        EnvPrefix = $config.envPrefix
        SupportedTargets = ($config.supportedTargets -join ',')
        Status = 'Passed'
    }
}

function Get-DragonReleaseVersion {
    [CmdletBinding()]
    param(
        [string]$Version,
        [object]$Config
    )

    $value = if ([string]::IsNullOrWhiteSpace($Version)) {
        [string]$Config.version
    } else {
        $Version
    }

    $value = $value.Trim()
    if ($value.StartsWith('v', [StringComparison]::OrdinalIgnoreCase)) {
        $value = $value.Substring(1)
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        throw 'Version cannot be empty.'
    }

    return [pscustomobject]@{
        Version = $value
        Tag = "v$value"
    }
}

function Get-DragonAndroidVersionCode {
    [CmdletBinding()]
    param(
        [int]$AndroidVersionCode = 0,
        [object]$Config,
        [string]$Version
    )

    if ($AndroidVersionCode -gt 0) {
        return $AndroidVersionCode
    }

    if ($Config -and $Config.PSObject.Properties.Name -contains 'androidVersionCode' -and [int]$Config.androidVersionCode -gt 0) {
        return [int]$Config.androidVersionCode
    }

    $numeric = ($Version -split '-', 2)[0]
    $parts = $numeric.Split('.') | ForEach-Object { [int]$_ }
    while ($parts.Count -lt 3) {
        $parts += 0
    }

    return (($parts[0] * 10000) + ($parts[1] * 100) + $parts[2])
}

function New-DragonQaSession {
    [CmdletBinding()]
    param(
        [string]$RepoRoot = (Get-Location).Path,
        [string]$TesterName = 'QA',
        [string]$BuildLabel,
        [string]$Platform = 'Windows Desktop',
        [string]$Viewport = '1440x900',
        [string]$OutputDirectory
    )

    $config = Get-DragonAppConfig -RepoRoot $RepoRoot
    $release = Get-DragonReleaseVersion -Version $null -Config $config
    if ([string]::IsNullOrWhiteSpace($BuildLabel)) {
        $BuildLabel = "$($config.productName) $($release.Version) local"
    }

    $root = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        Join-Path $RepoRoot 'docs\qa\sessions'
    } else {
        $OutputDirectory
    }

    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $safeProduct = ([string]$config.shortName).ToLowerInvariant()
    $path = Join-Path $root "$safeProduct-qa-session-$timestamp.md"

    $content = @"
# $($config.productName) QA Session

| Field | Value |
| --- | --- |
| Tester | $TesterName |
| Build | $BuildLabel |
| Platform | $Platform |
| Viewport | $Viewport |
| Date | $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz') |

## Checks

- [ ] First launch opens without crash.
- [ ] Onboarding or first-use state is understandable.
- [ ] Core workflow can create, edit, persist, close, and reopen data.
- [ ] Local data ownership path is visible or documented.
- [ ] No clipped text, hidden primary action, or overlapping controls at the stated viewport.
- [ ] Export/backup/support flow is safe for private data.

## Findings

| Severity | Area | Finding | Evidence | Status |
| --- | --- | --- | --- | --- |
|  |  |  |  |  |
"@

    Set-Content -Path $path -Value $content -Encoding utf8
    return (Resolve-Path $path).Path
}

function New-DragonReleaseManifest {
    [CmdletBinding()]
    param(
        [string]$RepoRoot = (Get-Location).Path,
        [string]$ReleaseRoot,
        [string]$Version,
        [string[]]$Platforms = @('Desktop', 'Browser', 'Android'),
        [string]$SigningStatus = 'Unsigned',
        [bool]$TestsRun = $false,
        [string[]]$KnownCaveats = @()
    )

    $config = Get-DragonAppConfig -RepoRoot $RepoRoot
    $release = Get-DragonReleaseVersion -Version $Version -Config $config
    if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
        $ReleaseRoot = Join-Path $RepoRoot (Join-Path 'artifacts\releases' $release.Tag)
    }

    New-Item -ItemType Directory -Path $ReleaseRoot -Force | Out-Null
    $files = if (Test-Path $ReleaseRoot) {
        Get-ChildItem -Path $ReleaseRoot -Recurse -File |
            Where-Object { $_.FullName -notmatch '\\release-manifest\.json$' }
    } else {
        @()
    }

    $artifacts = @()
    foreach ($file in $files) {
        $relative = $file.FullName.Substring((Resolve-Path $ReleaseRoot).Path.Length).TrimStart('\', '/')
        $artifacts += [ordered]@{
            path = $relative
            sizeBytes = $file.Length
            sha256 = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash
        }
    }

    $manifest = [ordered]@{
        productName = $config.productName
        appId = $config.appId
        version = $release.Version
        tag = $release.Tag
        generatedAt = (Get-Date).ToUniversalTime().ToString('o')
        platforms = $Platforms
        signingStatus = $SigningStatus
        testsRun = $TestsRun
        knownCaveats = $KnownCaveats
        artifacts = $artifacts
    }

    $path = Join-Path $ReleaseRoot 'release-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $path -Encoding utf8
    return (Resolve-Path $path).Path
}

function New-DragonReleaseCandidateGate {
    [CmdletBinding()]
    param(
        [string]$RepoRoot = (Get-Location).Path,
        [string]$Version,
        [string]$ReleaseRoot,
        [string]$SigningStatus = 'Unsigned',
        [switch]$RunScreenshotQa,
        [switch]$RunCleanSlateQa,
        [switch]$RunPerformanceQa,
        [switch]$RequireSignedArtifacts,
        [switch]$PlanOnly
    )

    $config = Get-DragonAppConfig -RepoRoot $RepoRoot
    $release = Get-DragonReleaseVersion -Version $Version -Config $config
    if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
        $ReleaseRoot = Join-Path $RepoRoot (Join-Path 'artifacts\qa\release-candidate' $release.Tag)
    }

    New-Item -ItemType Directory -Path $ReleaseRoot -Force | Out-Null
    $checks = @(
        @{ area = 'Build and tests'; status = if ($PlanOnly) { 'Planned' } else { 'PendingEvidence' }; blocksProduction = $true },
        @{ area = 'Version metadata'; status = 'PendingManualReview'; blocksProduction = $true },
        @{ area = 'Package identities'; status = 'PendingManualReview'; blocksProduction = $true },
        @{ area = 'Artifact hashes'; status = 'PendingEvidence'; blocksProduction = $true },
        @{ area = 'Screenshot QA'; status = if ($RunScreenshotQa) { 'Planned' } else { 'Pending' }; blocksProduction = $true },
        @{ area = 'Clean-slate QA'; status = if ($RunCleanSlateQa) { 'Planned' } else { 'Pending' }; blocksProduction = $true },
        @{ area = 'Performance QA'; status = if ($RunPerformanceQa) { 'Planned' } else { 'Pending' }; blocksProduction = $true },
        @{ area = 'Backup/restore QA'; status = 'PendingManualReview'; blocksProduction = $true },
        @{ area = 'Privacy and local data ownership'; status = 'PendingManualReview'; blocksProduction = $true },
        @{ area = 'Accessibility review'; status = 'PendingManualSignOff'; blocksProduction = $true },
        @{ area = 'Final manual sign-off'; status = 'PendingManualSignOff'; blocksProduction = $true }
    )

    if ($RequireSignedArtifacts -and $SigningStatus -ne 'Signed') {
        $checks += @{ area = 'Signing requirement'; status = 'Blocked'; blocksProduction = $true }
    }

    $gate = [ordered]@{
        productName = $config.productName
        version = $release.Version
        generatedAt = (Get-Date).ToUniversalTime().ToString('o')
        planOnly = [bool]$PlanOnly
        signingStatus = $SigningStatus
        checks = $checks
    }

    $jsonPath = Join-Path $ReleaseRoot 'release-candidate-gate.json'
    $gate | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding utf8

    $markdownPath = Join-Path $ReleaseRoot 'release-candidate-gate.md'
    $lines = @(
        "# $($config.productName) Release Candidate Gate",
        '',
        "| Field | Value |",
        "| --- | --- |",
        "| Version | $($release.Version) |",
        "| Signing | $SigningStatus |",
        "| Plan only | $([bool]$PlanOnly) |",
        '',
        "| Area | Status | Blocks Production |",
        "| --- | --- | --- |"
    )

    foreach ($check in $checks) {
        $lines += "| $($check.area) | $($check.status) | $($check.blocksProduction) |"
    }

    Set-Content -Path $markdownPath -Value ($lines -join [Environment]::NewLine) -Encoding utf8
    return [pscustomobject]@{
        Json = (Resolve-Path $jsonPath).Path
        Markdown = (Resolve-Path $markdownPath).Path
    }
}

function Test-DragonPowerShellSyntax {
    [CmdletBinding()]
    param(
        [string]$RepoRoot = (Get-Location).Path,
        [string]$Path = 'scripts'
    )

    $target = Join-Path $RepoRoot $Path
    if (-not (Test-Path $target)) {
        return @()
    }

    $results = @()
    foreach ($file in Get-ChildItem -Path $target -Filter '*.ps1' -Recurse -File) {
        $tokens = $null
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        $results += [pscustomobject]@{
            Path = $file.FullName
            ErrorCount = $errors.Count
            Status = if ($errors.Count -eq 0) { 'Passed' } else { 'Failed' }
            Errors = ($errors | ForEach-Object { $_.Message }) -join '; '
        }
    }

    $failed = $results | Where-Object { $_.ErrorCount -gt 0 }
    if ($failed.Count -gt 0) {
        $details = ($failed | ForEach-Object { "$($_.Path): $($_.Errors)" }) -join [Environment]::NewLine
        throw "PowerShell syntax check failed:$([Environment]::NewLine)$details"
    }

    return $results
}

Export-ModuleMember -Function @(
    'Find-DragonAppKitRoot',
    'Get-DragonAppConfig',
    'Get-DragonReleaseVersion',
    'Get-DragonAndroidVersionCode',
    'New-DragonQaSession',
    'New-DragonReleaseCandidateGate',
    'New-DragonReleaseManifest',
    'Test-DragonPowerShellSyntax',
    'Test-DragonAppContract'
)


@{
    RootModule = 'DragonRelease.psm1'
    ModuleVersion = '0.1.0'
    GUID = '0db53bd8-28a5-4982-bf96-3b31f184c0fa'
    Author = 'Dragon AppKit'
    CompanyName = 'Dragon AppKit'
    Copyright = '(c) Dragon AppKit'
    Description = 'Shared release, QA, and manifest helpers for Dragon apps.'
    PowerShellVersion = '5.1'
    FunctionsToExport = @(
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
}


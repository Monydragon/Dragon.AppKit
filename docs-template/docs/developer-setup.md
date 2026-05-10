# Developer Setup

## Prerequisites

- .NET 10 SDK
- PowerShell 5.1 or newer
- Git
- Optional Android workload and SDK for Android builds
- Optional WebAssembly workload for Browser builds

## Standard Loop

```powershell
dotnet restore <solution>
dotnet build <solution> --no-restore
dotnet test <solution> --no-build
.\scripts\desktop-smoke-test.ps1 -NoBuild
```

## Release Loop

```powershell
.\scripts\release-candidate-gate.ps1 -Version <version> -PlanOnly
.\scripts\publish-releases.ps1 -Version <version> -Platform Desktop,Browser,Android
```


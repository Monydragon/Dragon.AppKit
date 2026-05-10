# Release Builds

```powershell
.\scripts\release-candidate-gate.ps1 -Version 0.1.0 -PlanOnly
.\scripts\publish-releases.ps1 -Version 0.1.0 -Platform Desktop,Browser,Android
```


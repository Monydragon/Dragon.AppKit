# Developer Setup

```powershell
dotnet restore DragonTemplate.slnx
dotnet build DragonTemplate.slnx --no-restore
dotnet test DragonTemplate.slnx --no-build
.\scripts\desktop-smoke-test.ps1 -NoBuild
```

Browser:

```powershell
dotnet publish DragonTemplate.Browser\DragonTemplate.Browser.csproj -c Release
```

Android:

```powershell
dotnet build DragonTemplate.Android\DragonTemplate.Android.csproj -c Release
```


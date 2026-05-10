# Dragon Template

Dragon Template is a local-first Avalonia app scaffolded from Dragon.AppKit.

## Start Here

```powershell
dotnet restore DragonTemplate.slnx
dotnet build DragonTemplate.slnx --no-restore
dotnet test DragonTemplate.slnx --no-build
dotnet run --project DragonTemplate.Desktop
```

## App Contract

Product metadata lives in `dragon-app.json`. Release and QA scripts use that file to keep names, versions, package ids, and environment variables aligned.


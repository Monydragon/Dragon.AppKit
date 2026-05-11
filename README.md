# Dragon.AppKit

Dragon.AppKit is the shared baseline for the Dragon family of local-first Avalonia apps.

It provides:

- an app contract: `dragon-app.json`
- a reusable release and QA PowerShell module: `build/DragonRelease`
- an installable app template: `templates/dragon-app`
- baseline docs for developer setup, data ownership, QA, release, assets, and troubleshooting

## Install The Template

```powershell
dotnet new install .\templates\dragon-app
```

Create a new app:

```powershell
dotnet new dragon-app `
  -n DragonFoo `
  --product "Dragon Foo" `
  --app-id com.dragonfoo.app `
  --env-prefix DRAGON_FOO `
  --targets Desktop,Android,Browser
```

## Product Repo Contract

Each Dragon product repo should include a root `dragon-app.json` file. Wrapper scripts can find this AppKit by either:

- setting `DRAGON_APPKIT_ROOT`, or
- placing `Dragon.AppKit` as a sibling directory beside the product repo.

## Validation Loop

```powershell
dotnet test
pwsh -NoProfile -File .\scripts\test-template.ps1
```

## Dragon Publisher GUI

Run the publishing utility:

```powershell
.\scripts\run-publisher.ps1
```

The utility scans a workspace for Dragon apps plus broader .NET/Avalonia/KNI-style project roots, detects supported heads and publish-script parameters, lets you edit release options, writes version metadata across app contracts and project files, checks latest release-folder version evidence, previews the generated publish plan, and runs the editable command with live output.

For multi-project release work, add scoped projects to the in-memory queue and run them sequentially. The progress overlay shows queue position, publish stage, percentage, elapsed time, and ETA.

It can also append an itch.io upload step using butler. Configure the butler path, itch user/game/channel, and source folder in the GUI, then enable `Publish with butler after build`.

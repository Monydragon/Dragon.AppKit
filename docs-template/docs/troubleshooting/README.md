# Troubleshooting

## Scripts Disabled

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-releases.ps1
```

## AppKit Not Found

Set:

```powershell
$env:DRAGON_APPKIT_ROOT='C:\Projects\Github\Avalonia\Dragon.AppKit'
```

## Browser Publish Warnings

Avalonia Browser/WebAssembly may emit trim warnings. Treat them as review items, then smoke test the published browser output.


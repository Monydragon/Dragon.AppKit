# Release Builds

The standard Dragon release root is:

```text
artifacts/releases/v<version>
```

Baseline production targets:

- Desktop: Windows, Linux, macOS x64, macOS arm64
- Browser: static WebAssembly publish output
- Android: APK for direct validation, AAB for Play Store upload

iOS may exist in product repos, but it is not a baseline blocking target until macOS signing is configured.


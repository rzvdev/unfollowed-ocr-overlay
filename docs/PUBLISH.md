# Publish Guide

Use these scripts to produce a shippable, self-contained build of the scanner app.

## Prerequisites

- .NET SDK 8.x
- Windows 10/11 for running the published build (Win32 capture/overlay)
- `zip` available on Linux/macOS when using the Bash script

## Quick Start

From the repository root:

### PowerShell
```powershell
./scripts/publish.ps1 -Runtime win-x64 -Configuration Release
```

### Bash
```bash
./scripts/publish.sh win-x64 Release
```

Artifacts land in:

```
./artifacts/publish/win-x64/<version>/
```

Distribution packages land in:

```
./artifacts/dist/Unfollowed-<version>-win-x64.zip
```

## Versioning & Changelog

- Update the release number in `Directory.Build.props`.
- Record changes in `CHANGELOG.md` under the matching version.

## Final Windows Distribution Steps

1. Update `Directory.Build.props` with the new version.
2. Add release notes to `CHANGELOG.md`.
3. Run the publish script for the target runtime.
4. Share the generated zip from `artifacts/dist`.

## Notes

- The publish scripts create a self-contained, single-file executable and zip it with the changelog.
- Update `src/Unfollowed.App/appsettings.json` to tune FPS, preprocess profiles, OCR language, or stabilizer settings before shipping.

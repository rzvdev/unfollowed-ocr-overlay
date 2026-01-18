# Publish Guide

Use these scripts to produce a shippable, self-contained build of the scanner app.

## Prerequisites

- .NET SDK 8.x
- Windows 10/11 for running the published build (Win32 capture/overlay)

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
./artifacts/publish/win-x64/
```

## Notes

- The publish scripts create a self-contained, single-file executable.
- Update `src/Unfollowed.App/appsettings.json` to tune FPS, preprocess profiles, OCR language, or stabilizer settings before shipping.

# Build Guide

## Supported Build Modes

- Debug
- Release

---

## Build Using CLI

From the repository root:

### Debug Build
```bash
dotnet build -c Debug
```

### Release Build
```bash
dotnet build -c Release
```

Artifacts will be generated under:
```
/src/Unfollowed.App/bin/
```

---

## Build Using Visual Studio

1. Open `Unfollowed.sln`
2. Select configuration:
   - Debug or Release
3. Select platform:
   - x64
4. Press **Build Solution**

---

## Running the Application

### From CLI
```bash
dotnet run --project src/Unfollowed.App
```

### From Visual Studio
- Set `Unfollowed.App` as Startup Project
- Press **F5** or **Ctrl+F5**

---

## CLI Project (Cross-Platform)

The cross-platform CLI lives in `src/Unfollowed.Cli`. It provides headless data commands only.

### Build
```bash
dotnet build -c Release src/Unfollowed.Cli
```

### Run
```bash
dotnet run --project src/Unfollowed.Cli -- <command> [args]
```

### Supported Commands (Cross-Platform)
- **compute** — Compute NonFollowBack counts from CSV exports.
- **convert-json** — Export Instagram JSON to CSV.

Examples:
```bash
dotnet run --project src/Unfollowed.Cli -- compute data/following.csv data/followers.csv
dotnet run --project src/Unfollowed.Cli -- convert-json data/following.json data/followers.json data/out
```

---

## Windows CLI Commands (Win32 Capture/Overlay Required)

The Windows UI app (`src/Unfollowed.App`) exposes additional CLI-only commands for scan/overlay workflows.
These require the Windows runtime (Win32 capture/overlay).

### Run (Windows)
```bash
dotnet run --project src/Unfollowed.App -- <command> [args]
```

### Supported Commands (Windows-only)
- **scan** — Start scan loop (requires `--roi`).
- **scan-csv** — Start scan loop using CSV inputs.
- **overlay-test** — Show click-through overlay for alignment.
- **overlay-calibrate** — Show ROI guides for calibration.
- **capture-test** — Capture 1–3 ROI frames to BMP on disk.
- **ocr-test** — Run capture/preprocess/OCR once and print tokens.
- **settings** — View or update stored settings (CLI-only).

Examples:
```bash
dotnet run --project src/Unfollowed.App -- scan --roi 200,150,800,600
dotnet run --project src/Unfollowed.App -- scan-csv data/following.csv data/followers.csv --roi 200,150,800,600
dotnet run --project src/Unfollowed.App -- overlay-test 200 150 800 600
dotnet run --project src/Unfollowed.App -- capture-test 200 150 800 600 3 --preprocess
dotnet run --project src/Unfollowed.App -- settings --reset
```

---

## Build Configuration Notes

- Target framework: `net8.0-windows`
- Windows Forms enabled
- Self-contained build optional
- Single-file publish supported (see `scripts/publish.*`)
  - Version is defined in `Directory.Build.props`
  - Release notes live in `CHANGELOG.md`

---

## Publishing (Optional)

Example self-contained release build:

```bash
dotnet publish src/Unfollowed.App   -c Release   -r win-x64   --self-contained true   /p:PublishSingleFile=true
```

Output:
```
/bin/Release/net8.0-windows10.0.22621.0/win-x64/publish/
```

---

## Common Build Issues

### OCR Engine Fails to Initialize
- Verify runtime dependencies
- Ensure correct CPU architecture (x64)

### Overlay Not Visible
- Check DPI scaling
- Ensure correct screen selected
- Disable fullscreen-exclusive apps

---

## CI Compatibility

- GitHub Actions supported
- Windows runners required
- No secrets required

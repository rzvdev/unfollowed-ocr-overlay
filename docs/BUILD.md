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
/src/UnfollowedOcrOverlay.UI/bin/
```

---

## Build Using Visual Studio

1. Open `UnfollowedOcrOverlay.sln`
2. Select configuration:
   - Debug or Release
3. Select platform:
   - x64
4. Press **Build Solution**

---

## Running the Application

### From CLI
```bash
dotnet run --project src/UnfollowedOcrOverlay.UI
```

### From Visual Studio
- Set `UnfollowedOcrOverlay.UI` as Startup Project
- Press **F5** or **Ctrl+F5**

---

## Build Configuration Notes

- Target framework: `net8.0-windows`
- WPF enabled
- Self-contained build optional
- Single-file publish supported

---

## Publishing (Optional)

Example self-contained release build:

```bash
dotnet publish src/UnfollowedOcrOverlay.UI   -c Release   -r win-x64   --self-contained true   /p:PublishSingleFile=true
```

Output:
```
/bin/Release/net8.0-windows/win-x64/publish/
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

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

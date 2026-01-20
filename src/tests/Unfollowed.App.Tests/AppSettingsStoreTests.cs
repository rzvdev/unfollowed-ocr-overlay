using Unfollowed.App.Settings;
using Unfollowed.Capture;
using Unfollowed.Overlay;

namespace Unfollowed.App.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsSettings()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var settingsPath = Path.Combine(tempDir, "settings.json");
        var store = new AppSettingsStore(settingsPath);

        var settings = new AppSettings(
            TargetFps: 7,
            OcrFrameDiffThreshold: 0.12f,
            OcrMinTokenConfidence: 0.33f,
            StabilizerConfidenceThreshold: 0.44f,
            Roi: new RoiSelection(10, 20, 300, 400, 1),
            Theme: OverlayTheme.Amber,
            ThemeMode: ThemeMode.System,
            ShowRoiOutline: true);

        try
        {
            store.Save(settings);
            var loaded = store.Load(new AppSettings(1, 0.01f, 0.0f, 0.1f, null, OverlayTheme.Lime, ThemeMode: ThemeMode.System, ShowRoiOutline: false));

            Assert.Equal(settings, loaded);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}

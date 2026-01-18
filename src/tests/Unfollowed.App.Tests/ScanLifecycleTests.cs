using Microsoft.Extensions.DependencyInjection;
using Unfollowed.App.Scan;
using Unfollowed.Capture;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Models;
using Unfollowed.Core.Stabilization;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Tests;

public sealed class ScanLifecycleTests
{
    [Fact]
    public async Task Scan_Starts_And_Stops_Cleanly()
    {
        var sp = AppHost.BuildServiceProvider();
        var controller = sp.GetRequiredService<IScanSessionController>();

        var roi = new RoiSelection(0, 0, 100, 100);

        var data = new NonFollowBackData(
            Following: Array.Empty<string>(),
            Followers: Array.Empty<string>(),
            NonFollowBack: Array.Empty<string>(),
            FollowingStats: new CsvImportStats(0, 0, 0, 0),
            FollowersStats: new CsvImportStats(0, 0, 0, 0)
        );

        var options = new ScanSessionOptions(
            TargetFps: 4,
            OcrFrameDiffThreshold: 0.0f,
            Preprocess: new PreprocessOptions(),
            Ocr: new OcrOptions(),
            Extraction: new ExtractionOptions(),
            Stabilizer: new StabilizerOptions(),
            Overlay: new OverlayOptions()
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await controller.StartAsync(data, roi, options, cts.Token);

        // Let it run briefly
        await Task.Delay(200);

        await controller.StopAsync(CancellationToken.None);
    }
}

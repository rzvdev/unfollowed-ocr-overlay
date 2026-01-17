using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Overlay;
using Unfollowed.Overlay.Win32;

namespace Unfollowed.App.Scan;

public sealed class ScanSessionController : IScanSessionController
{

    private readonly IOverlayRenderer _overlay;

    public ScanSessionController(IOverlayRenderer overlay)
    {
        _overlay = overlay;
    }

    public async Task StartAsync(NonFollowBackData data, RoiSelection roi, ScanSessionOptions options, CancellationToken ct)
    {
        await _overlay.InitializeAsync(roi, options.Overlay, ct);

        var highlights = new[]
        {
            new Highlight("top_left", 0.99f, new RectF(roi.X + 20, roi.Y + 20, 180, 28), true),
            new Highlight("middle", 0.99f, new RectF(roi.X + 60, roi.Y + 180, 220, 28), true),
            new Highlight("bottom_right", 0.99f, new RectF(roi.X + roi.Width - 260, roi.Y + roi.Height - 60, 240, 28), true),
        };

        await _overlay.RenderAsync(highlights, ct);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _overlay.ClearAsync(ct);
        await _overlay.DisposeAsync();
    }
}

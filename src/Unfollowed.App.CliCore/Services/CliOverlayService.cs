using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Overlay;

namespace Unfollowed.App.Services;

public sealed class CliOverlayService : IOverlayService
{
    private readonly IOverlayRenderer _renderer;
    private RoiSelection? _roi;

    public CliOverlayService(IOverlayRenderer renderer)
    {
        _renderer = renderer;
    }

    public Task SetRoiAsync(RoiSelection roi, CancellationToken ct)
    {
        _roi = roi;
        return Task.CompletedTask;
    }

    public Task InitializeAsync(OverlayOptions options, CancellationToken ct)
    {
        if (_roi is null)
        {
            throw new InvalidOperationException("ROI must be set before initializing overlays.");
        }

        return _renderer.InitializeAsync(_roi, options, ct);
    }

    public Task UpdateHighlightsAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct)
        => _renderer.RenderAsync(highlights, ct);

    public Task ClearAsync(CancellationToken ct)
        => _renderer.ClearAsync(ct);

    public ValueTask DisposeAsync()
        => _renderer.DisposeAsync();
}

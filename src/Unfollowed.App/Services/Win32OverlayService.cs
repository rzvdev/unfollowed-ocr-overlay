using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Overlay;
using Unfollowed.Overlay.Win32;

namespace Unfollowed.App.Services;

public sealed class Win32OverlayService : IOverlayService
{
    private Win32OverlayRenderer? _renderer;
    private RoiSelection? _roi;
    private OverlayOptions? _options;

    public async Task SetRoiAsync(RoiSelection roi, CancellationToken ct)
    {
        _roi = roi;
        if (_options is not null)
        {
            await ResetRendererAsync(ct);
        }
    }

    public async Task InitializeAsync(OverlayOptions options, CancellationToken ct)
    {
        _options = options;
        if (_roi is null)
        {
            throw new InvalidOperationException("ROI must be selected before initializing the overlay.");
        }

        await ResetRendererAsync(ct);
    }

    public async Task UpdateHighlightsAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct)
    {
        var renderer = _renderer ?? throw new InvalidOperationException("Overlay has not been initialized.");
        await renderer.RenderAsync(highlights, ct);
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        if (_renderer is null)
        {
            return;
        }

        await _renderer.ClearAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_renderer is null)
        {
            return;
        }

        await _renderer.DisposeAsync();
        _renderer = null;
    }

    private async Task ResetRendererAsync(CancellationToken ct)
    {
        if (_renderer is not null)
        {
            await _renderer.DisposeAsync();
        }

        _renderer = new Win32OverlayRenderer();
        await _renderer.InitializeAsync(_roi!, _options!, ct);
    }
}

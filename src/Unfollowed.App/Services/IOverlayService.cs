using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Overlay;

namespace Unfollowed.App.Services;

public interface IOverlayService : IAsyncDisposable
{
    Task SetRoiAsync(RoiSelection roi, CancellationToken ct);
    Task InitializeAsync(OverlayOptions options, CancellationToken ct);
    Task UpdateHighlightsAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}

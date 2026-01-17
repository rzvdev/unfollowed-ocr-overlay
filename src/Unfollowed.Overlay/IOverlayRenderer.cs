using Unfollowed.Capture;
using Unfollowed.Core.Models;

namespace Unfollowed.Overlay;

public interface IOverlayRenderer
{
    Task InitializeAsync(RoiSelection roi, OverlayOptions options, CancellationToken ct);
    Task RenderAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}

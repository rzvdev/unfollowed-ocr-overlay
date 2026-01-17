using Unfollowed.Capture;
using Unfollowed.Core.Models;

namespace Unfollowed.Overlay;

public sealed class NullOverlayRenderer : IOverlayRenderer
{
    public Task InitializeAsync(RoiSelection roi, OverlayOptions options, CancellationToken ct) => Task.CompletedTask;
    public Task RenderAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct) => Task.CompletedTask;
    public Task ClearAsync(CancellationToken ct) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

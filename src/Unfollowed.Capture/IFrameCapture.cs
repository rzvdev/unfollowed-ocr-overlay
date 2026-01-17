namespace Unfollowed.Capture;

public interface IFrameCapture : IAsyncDisposable
{
    Task InitializeAsync(RoiSelection roi, CancellationToken ct);

    Task<CaptureFrame> CaptureAsync(CancellationToken ct);
}

namespace Unfollowed.Capture;

public sealed class NullFrameCapture : IFrameCapture
{
    public Task InitializeAsync(RoiSelection roi, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task<CaptureFrame> CaptureAsync(CancellationToken ct)
    {
        throw new NotSupportedException("Null frame capture cannot capture frames.");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

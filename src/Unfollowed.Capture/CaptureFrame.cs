namespace Unfollowed.Capture;

public sealed record CaptureFrame(byte[] Bgra32, int Width, int Height, long TimestampUtcTicks);

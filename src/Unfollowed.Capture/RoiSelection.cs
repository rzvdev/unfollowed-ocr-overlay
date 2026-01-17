namespace Unfollowed.Capture;

public sealed record RoiSelection(
    int X,
    int Y,
    int Width,
    int Height,
    int MonitorId = 0
);

namespace Unfollowed.App.Scan;

public sealed record CaptureDumpOptions(
    bool Enabled = false,
    int DumpEveryNFrames = 0,
    string OutputDirectory = "frame_dumps"
);

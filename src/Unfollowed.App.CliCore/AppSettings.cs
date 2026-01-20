using Unfollowed.Capture;
using Unfollowed.Overlay;

namespace Unfollowed.App.Settings;

public sealed record AppSettings(
    int TargetFps,
    float OcrFrameDiffThreshold,
    float OcrMinTokenConfidence,
    float StabilizerConfidenceThreshold,
    bool AllowUncertainHighlights,
    RoiSelection? Roi,
    OverlayTheme Theme,
    ThemeMode ThemeMode,
    bool ShowRoiOutline
);

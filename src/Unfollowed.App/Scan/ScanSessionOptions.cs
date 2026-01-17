using Unfollowed.Core.Extraction;
using Unfollowed.Core.Stabilization;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Scan;

public sealed record ScanSessionOptions(
    int TargetFps,
    PreprocessOptions Preprocess,
    OcrOptions Ocr,
    ExtractionOptions Extraction,
    StabilizerOptions Stabilizer,
    OverlayOptions Overlay
);

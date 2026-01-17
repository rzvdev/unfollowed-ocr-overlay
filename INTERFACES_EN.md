# GUI Unfollowed (OCR) — Public Interfaces & Contracts

This document defines the public contracts (interfaces, models, options) between modules:
- Csv
- Capture
- Preprocess
- OCR
- Matching/Stabilizer (Core)
- Overlay
- App Orchestration

Target: .NET 8, Windows.

---

## 1) Core Models (GuiUnfollowed.Core)

### 1.1 Username normalization
```csharp
namespace GuiUnfollowed.Core;

public interface IUsernameNormalizer
{
    string Normalize(string raw);
}

public sealed record UsernameNormalizationOptions(
    bool ToLower = true,
    bool StripLeadingAt = true,
    int MinLength = 1,
    int MaxLength = 30,
    string AllowedCharsRegex = "^[a-z0-9._]+$"
);
```

### 1.2 Sets & match results
```csharp
namespace GuiUnfollowed.Core;

public sealed record CsvImportStats(
    int TotalRows,
    int ValidUsernames,
    int InvalidRows,
    int DuplicatesIgnored
);

public sealed record NonFollowBackData(
    IReadOnlyCollection<string> Following,
    IReadOnlyCollection<string> Followers,
    IReadOnlyCollection<string> NonFollowBack,
    CsvImportStats FollowingStats,
    CsvImportStats FollowersStats
);

public sealed record MatchCandidate(
    string UsernameNormalized,
    float Confidence,
    RectF RoiRect // coordinates in ROI-space (float for flexibility)
);

public sealed record Highlight(
    string UsernameNormalized,
    float Confidence,
    RectF ScreenRect, // coordinates in screen-space
    bool IsCertain
);

public readonly record struct RectF(float X, float Y, float W, float H);
```

### 1.3 Stabilizer (K out of M)
```csharp
namespace GuiUnfollowed.Core;

public sealed record StabilizerOptions(
    int WindowSizeM = 3,
    int RequiredK = 2,
    float ConfidenceThreshold = 0.60f,
    bool AllowUncertainHighlights = false
);

public interface IHighlightStabilizer
{
    IReadOnlyList<Highlight> Update(
        IReadOnlyList<MatchCandidate> currentFrameCandidates,
        RoiToScreenTransform transform,
        StabilizerOptions options
    );

    void Reset();
}

public sealed record RoiToScreenTransform(
    float RoiX, float RoiY, float RoiW, float RoiH,
    float FrameW, float FrameH
)
{
    public RectF ToScreen(RectF roiRect)
    {
        var scaleX = RoiW / FrameW;
        var scaleY = RoiH / FrameH;
        return new RectF(
            RoiX + roiRect.X * scaleX,
            RoiY + roiRect.Y * scaleY,
            roiRect.W * scaleX,
            roiRect.H * scaleY
        );
    }
}
```

---

## 2) CSV Module (GuiUnfollowed.Csv)

### 2.1 Import contract
```csharp
namespace GuiUnfollowed.Csv;

public sealed record CsvImportOptions(
    string? UsernameColumnHint = null,       // e.g.: "username"
    bool HasHeader = true,
    char Delimiter = ',',
    int MaxRows = 200_000
);

public sealed record CsvImportResult(
    IReadOnlyCollection<string> UsernamesNormalized,
    GuiUnfollowed.Core.CsvImportStats Stats,
    string? DetectedUsernameColumn
);

public interface ICsvImporter
{
    CsvImportResult ImportUsernames(string csvPath, CsvImportOptions options, CancellationToken ct);
}
```

### 2.2 NonFollowBack calculator
```csharp
namespace GuiUnfollowed.Csv;

public interface INonFollowBackCalculator
{
    GuiUnfollowed.Core.NonFollowBackData Compute(
        CsvImportResult following,
        CsvImportResult followers
    );
}
```

---

## 3) Capture Module (GuiUnfollowed.Capture)

### 3.1 ROI selection (region-first)
```csharp
namespace GuiUnfollowed.Capture;

public sealed record RoiSelection(
    int X, int Y, int Width, int Height,
    int MonitorId = 0
);

public interface IRoiSelector
{
    // UI-driven; the implementation may live in the App layer, but the contract stays here
    Task<RoiSelection> SelectRegionAsync(CancellationToken ct);
}
```

### 3.2 Frame capture contract
```csharp
namespace GuiUnfollowed.Capture;

public sealed record CaptureFrame(
    byte[] Bgra32,     // raw BGRA32 buffer (or pointer/IMemoryOwner in the implementation)
    int Width,
    int Height,
    long TimestampUtcTicks
);

public interface IFrameCapture : IAsyncDisposable
{
    RoiSelection Roi { get; }
    Task InitializeAsync(RoiSelection roi, CancellationToken ct);
    ValueTask<CaptureFrame> CaptureAsync(CancellationToken ct);
}
```

---

## 4) Preprocess Module (GuiUnfollowed.Preprocess)

```csharp
namespace GuiUnfollowed.Preprocess;

public enum PreprocessProfile
{
    Default,
    LightUi,
    DarkUi,
    HighContrast
}

public sealed record PreprocessOptions(
    PreprocessProfile Profile = PreprocessProfile.Default,
    float Contrast = 1.0f,
    float Sharpen = 0.0f
);

public sealed record ProcessedFrame(
    byte[] Gray8,   // preprocessed grayscale 8-bit image
    int Width,
    int Height
);

public interface IFramePreprocessor
{
    ProcessedFrame Process(GuiUnfollowed.Capture.CaptureFrame frame, PreprocessOptions options);
}
```

---

## 5) OCR Module (GuiUnfollowed.Ocr)

### 5.1 OCR provider interface (engine-agnostic)
```csharp
namespace GuiUnfollowed.Ocr;

public sealed record OcrToken(
    string Text,
    GuiUnfollowed.Core.RectF RoiRect,
    float Confidence
);

public sealed record OcrResult(
    IReadOnlyList<OcrToken> Tokens,
    int FrameWidth,
    int FrameHeight
);

public sealed record OcrOptions(
    string LanguageTag = "en",
    float MinTokenConfidence = 0.0f,
    string? CharacterWhitelist = "abcdefghijklmnopqrstuvwxyz0123456789._@"
);

public interface IOcrProvider
{
    Task<OcrResult> RecognizeAsync(
        GuiUnfollowed.Preprocess.ProcessedFrame frame,
        OcrOptions options,
        CancellationToken ct
    );
}
```

---

## 6) Matching/Extraction Module (GuiUnfollowed.Core or separate GuiUnfollowed.Matching)

```csharp
namespace GuiUnfollowed.Core;

public sealed record ExtractionOptions(
    int MaxUsernameLength = 30,
    bool RequireLeadingAtOptional = true,
    string CandidateRegex = "@?[a-zA-Z0-9._]{1,30}",
    IReadOnlyCollection<string>? StopWords = null
);

public interface IUsernameExtractor
{
    IReadOnlyList<MatchCandidate> ExtractCandidates(
        GuiUnfollowed.Ocr.OcrResult ocr,
        ExtractionOptions options,
        Func<string, bool> isInNonFollowBackSet // membership check
    );
}
```

---

## 7) Overlay Module (GuiUnfollowed.Overlay)

```csharp
namespace GuiUnfollowed.Overlay;

public sealed record OverlayOptions(
    bool AlwaysOnTop = true,
    bool ClickThrough = true,
    bool ShowBadgeText = true
);

public interface IOverlayRenderer : IAsyncDisposable
{
    Task InitializeAsync(GuiUnfollowed.Capture.RoiSelection roi, OverlayOptions options, CancellationToken ct);

    // Draws the current highlights; the implementation may use double-buffering to reduce flicker
    Task RenderAsync(IReadOnlyList<GuiUnfollowed.Core.Highlight> highlights, CancellationToken ct);

    Task ClearAsync(CancellationToken ct);
}
```

---

## 8) App Orchestration (GuiUnfollowed.App)

### 8.1 Session controller (happy path)
```csharp
namespace GuiUnfollowed.App;

public sealed record ScanSessionOptions(
    int TargetFps = 4,
    GuiUnfollowed.Preprocess.PreprocessOptions Preprocess,
    GuiUnfollowed.Ocr.OcrOptions Ocr,
    GuiUnfollowed.Core.ExtractionOptions Extraction,
    GuiUnfollowed.Core.StabilizerOptions Stabilizer,
    GuiUnfollowed.Overlay.OverlayOptions Overlay
);

public interface IScanSessionController
{
    Task StartAsync(
        GuiUnfollowed.Core.NonFollowBackData data,
        GuiUnfollowed.Capture.RoiSelection roi,
        ScanSessionOptions options,
        CancellationToken ct
    );

    Task StopAsync(CancellationToken ct);
}
```

---

## 9) Configuration & defaults

An `appsettings.json` file is recommended, with sections for:
- CsvImportOptions
- ScanSessionOptions
- StopWords list

In the MVP, values can be hard-coded and later moved into configuration.

# Configuration Reference

Configuration for the application lives in `src/Unfollowed.App/appsettings.json`. This file is loaded on
startup and can be adjusted before launching the app.

## CsvImport

Controls how CSV files are parsed.

- `HasHeader`: When `true`, the importer skips the first row as a header.
- `Delimiter`: The character used to split CSV columns (for example, `,`).
- `MaxRows`: Upper limit on the number of rows read from a CSV file.

## Scan

Frame capture behavior for the OCR overlay.

- `TargetFps`: Target frames per second for screen capture and OCR processing.

## Preprocess

Image preprocessing settings applied before OCR runs.

- `Profile`: Named preprocessing profile (for example, `Default`).
- `Contrast`: Contrast multiplier applied to the captured frame.
- `Sharpen`: Sharpening strength applied during preprocessing.

## Ocr

OCR engine configuration.

- `LanguageTag`: BCP-47 language tag used by the OCR engine (for example, `en`).
- `MinTokenConfidence`: Minimum per-token confidence value to accept OCR output.
- `CharacterWhitelist`: Allowed characters for OCR tokens.
- `AssumedTokenConfidence`: Default confidence assigned when the engine omits a confidence score.

## Stabilizer

Controls temporal stabilization of OCR highlights across frames.

- `WindowSizeM`: Sliding window size used for stabilization.
- `RequiredK`: Minimum count of positive detections within the window.
- `ConfidenceThreshold`: Minimum confidence required to keep a highlight.
- `AllowUncertainHighlights`: When `true`, allows highlights below the confidence threshold.

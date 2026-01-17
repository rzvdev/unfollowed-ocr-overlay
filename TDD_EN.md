# docs/TDD.md

# GUI Unfollowed (OCR) — Technical Design Document (TDD)

## 1. Technical objective
Implement a desktop application that:
- reads 2 CSV files and computes `NonFollowBackSet`,
- captures a screen region (ROI) in real time,
- runs OCR on the ROI,
- maps OCR bounding boxes to screen coordinates,
- displays highlights via a transparent click-through overlay.

Target MVP: Windows 10/11.

---

## 2. High-level architecture

### 2.1 Main modules
1) **CsvModule**
   - CSV parsing
   - auto-detect username column
   - normalization + validation
   - output: `FollowingSet`, `FollowersSet`, `NonFollowBackSet`

2) **CaptureModule**
   - source selection (Window / Region)
   - ROI frame capture
   - output: `Frame` + metadata (timestamp, size, DPI info)

3) **PreprocessModule**
   - image pipeline: grayscale/contrast/threshold/denoise/sharpen
   - configurable profiles (Light/Dark/HighContrast)
   - output: `ProcessedFrame`

4) **OcrModule** (provider-based)
   - common interface: `IOcrProvider`
   - possible implementations:
     - Windows OCR
     - Tesseract
     - another engine
   - output: `OcrResult` (tokens/lines + bounding boxes + confidence)

5) **ExtractAndMatchModule**
   - handle extraction (regex + cleanup + stopwords)
   - token normalization
   - membership check against `NonFollowBackSet`
   - output: `MatchResult` (username, bbox, confidence, match=true/false)

6) **StabilizerModule**
   - temporal stabilization (K out of M)
   - per-frame deduplication
   - output: `StableHighlights`

7) **OverlayModule**
   - always-on-top transparent click-through window
   - rendering: rectangle + badge text
   - OCR→screen coordinate mapping
   - output: visual highlights

8) **UiModule**
   - control panel: import CSV, select ROI, start/stop, stats, settings

9) **LoggingModule**
   - structured logs (Info/Warn/Error/Debug)
   - optional: dump frames and OCR outputs in debug mode

### 2.2 Logical diagram (pipeline)
CSV → NonFollowBackSet  
ROI → CaptureFrame → Preprocess → OCR → Extract → Match → Stabilize → OverlayRender

---

## 3. Data model

### 3.1 Username normalization
- input: raw string
- output: normalized string  
Rules:
- trim
- remove leading '@'
- to lower
- remove anything not in `[a-z0-9._]`
- clamp length to 1..30

### 3.2 Recommended structures (conceptual)
- `HashSet<string> FollowingSet`
- `HashSet<string> FollowersSet`
- `HashSet<string> NonFollowBackSet`

- `Frame { Bitmap/Texture, Width, Height, Timestamp, DpiX, DpiY }`
- `OcrToken { Text, BBox(x,y,w,h) in ROI coords, Confidence }`
- `Highlight { Username, BBox in Screen coords, Confidence, IsCertain }`

---

## 4. Capture: ROI & coordinate mapping

### 4.1 ROI selection
Two modes:
- **Window capture**: select a window (e.g., browser).
- **Region capture**: select a rectangle on the screen.

MVP recommendation:
- provide “Select Region” (clearest for the user and for mapping).
- optional “Select Window” in v1.

### 4.2 DPI scaling and transforms
Problem: OCR coordinates are in the captured image space; the overlay draws in screen space. A robust transform is required.

Definitions:
- ROI in screen coordinates: `(roiX, roiY, roiW, roiH)` in “screen” pixels.
- Captured frame: `(frameW, frameH)` in “image” pixels.

If the frame is captured exactly from the ROI, then:
- `scaleX = roiW / frameW`
- `scaleY = roiH / frameH`
- `screenX = roiX + tokenX * scaleX`
- `screenY = roiY + tokenY * scaleY`
- `screenW = tokenW * scaleX`
- `screenH = tokenH * scaleY`

Important:
- On Windows with DPI scaling, “logical pixels” vs “physical pixels” can differ. The implementation must use a single consistent coordinate space (recommended: physical pixels) and obtain ROI coordinates from APIs that report values compatible with the capture engine.

### 4.3 Practical calibration (MVP)
Include a “Calibration Preview” step:
- the user selects the ROI
- the app displays a test overlay (e.g., an outline) exactly over the ROI
- the user confirms the overlay aligns correctly with the captured area

---

## 5. OCR Provider Interface (engine decoupling)

### 5.1 Interface
`IOcrProvider` must provide a recognition method that returns:
- structured text (tokens/lines),
- bounding boxes,
- confidence.

Capabilities:
- language: English (default) + optional multi-language
- character whitelist: configurable (preferred)

### 5.2 Recommended provider for MVP
- Windows OCR (simple setup, minimal install, decent performance)  
Constraint: bbox/confidence quality depends on the available API; if per-token bbox is not available, fall back to per-line bbox + tokenization within the line.

### 5.3 Alternative providers (v1/v2)
- Tesseract with tuning (PSM mode, whitelist, robust preprocessing)
- EasyOCR/PaddleOCR (better for some fonts, but heavier dependencies and footprint)

---

## 6. Image preprocessing (essential for accuracy)

### 6.1 Standard pipeline (“Default” profile)
1) Convert to grayscale  
2) Normalize contrast (CLAHE or linear contrast)  
3) Adaptive threshold (or Otsu)  
4) Denoise (light median/bilateral)  
5) Light sharpening (unsharp mask)

### 6.2 Profiles
- **Light UI**: moderate threshold, low denoise
- **Dark UI**: invert or different threshold, medium denoise
- **High contrast**: aggressive threshold, moderate sharpening

### 6.3 Configurability
UI-exposed settings:
- profile: Light/Dark/HighContrast
- threshold strength
- sharpen strength
- FPS

---

## 7. Extract & Match: robust rules

### 7.1 Stopwords (initial examples)
- following, followers, follow, message, remove, suggested, mutual, verified  
(Keep an extensible list in config.)

### 7.2 Regex & cleanup
- Candidate: `@?[a-zA-Z0-9._]{1,30}`
- Cleanup:
  - remove leading '@'
  - trim punctuation at the ends
  - whitelist `[a-z0-9._]`
- Final normalization (identical to CSV).

### 7.3 Confidence threshold
- Default: 0.60  
- If below threshold:
  - either do not highlight,
  - or highlight as “uncertain” (different style) — recommended for debugging but may confuse; MVP can start with “do not highlight”.

---

## 8. Temporal stabilization (anti-flicker)

### 8.1 Motivation
OCR varies frame-to-frame: a username may be read differently or may intermittently disappear.

### 8.2 K out of M strategy
Maintain a sliding window (last M frames) and a score per username:
- a username becomes “stable” if it appears in ≥ K frames.  
Default:
- M=3
- K=2

### 8.3 BBox association (simple tracking)
Instead of complex tracking, MVP can:
- take the bbox from the most recent frame where the username appeared,
- optionally average bboxes (optional) for smoothing.

---

## 9. Overlay: Windows implementation (click-through)

### 9.1 Overlay requirements
- Transparent background
- Always-on-top
- Click-through (does not intercept input)
- Fast rendering (no stutter)

### 9.2 Behavior
- overlay is repositioned/resized to cover the screen (or only the ROI) depending on implementation:
  - option A: full-screen overlay and draw only within ROI
  - option B: overlay exactly over the ROI (more efficient)

MVP recommendation: overlay exactly over the ROI (easier to correlate, better performance).

### 9.3 Highlight styles (MVP)
- rectangle outline + badge text “No Follow Back”
- optional: semi-transparent fill

Note: styling must not reduce readability of the “Unfollow” button.

---

## 10. UI / Control Panel

### 10.1 Screens and controls
- “Data” tab:
  - Upload Following CSV
  - Upload Followers CSV
  - Stats + NonFollowBack count
  - Export (optional)
- “Scanning” tab:
  - Select ROI
  - Preview ROI
  - Start/Stop
  - FPS slider
  - Profile (Light/Dark)
  - Confidence threshold
  - Debug toggles (show OCR text, dump frames)

### 10.2 Status messages
- “ROI not selected”
- “OCR running”
- “OCR stopped”
- “CSV invalid / Column not found”
- “DPI scaling detected: 150% (calibration recommended)”

---

## 11. Logging & Debugging

### 11.1 Logs (levels)
- INFO: import counts, start/stop, ROI set
- WARN: low-confidence spikes, OCR provider errors, empty frames
- ERROR: capture init fail, OCR crash
- DEBUG: token dumps, frame processing times

### 11.2 Debug artifacts (optional)
- save raw frame + preprocessed frame at intervals (e.g., 1/10 frames)
- export OCR tokens to JSON for offline analysis

---

## 12. Performance: processing loop

### 12.1 Per-frame timing targets
- Capture: 5–15 ms
- Preprocess: 5–20 ms
- OCR: 50–200 ms (engine-dependent)
- Match+stabilize: < 5 ms
- Render overlay: < 5 ms

### 12.2 MVP optimizations
- keep ROI as small as possible
- FPS limit (3–6)
- Adaptive OCR: run OCR only when there is significant change (frame-diff) — recommended for v1.

---

## 13. Test plan

### 13.1 Unit tests
- Username normalization (cases with @, spaces, uppercase, invalid characters)
- CSV parsing and username column auto-detection
- Matching logic (set difference)

### 13.2 Integration tests
- ROI selection + bbox mapping
- OCR provider returns tokens + bbox
- Stabilizer K out of M

### 13.3 E2E tests (manual)
- Instagram Web, following modal:
  - slow scroll / fast scroll
  - zoom 100/125/150
  - light/dark UI
- Multi-monitor with different DPI
- Confirmation: click-through overlay (you can press “Unfollow” without issues)

---

## 14. Security & compliance (pragmatic)
- No credentials are stored.
- CSV data remains local.
- Optional: “Clear session data” (clears sets and uploaded files from memory).
- Clear documentation that the user is responsible for unfollow actions and for complying with platform rules.

---

## 15. Recommended repo structure (docs + code)
- /docs
  - PRD.md
  - TDD.md
- /src
  - GuiUnfollowed.App (UI + DI)
  - GuiUnfollowed.Csv
  - GuiUnfollowed.Capture
  - GuiUnfollowed.Ocr
  - GuiUnfollowed.Overlay
  - GuiUnfollowed.Core (models + matching + stabilizer)
- /tests
  - GuiUnfollowed.Core.Tests
  - GuiUnfollowed.Csv.Tests

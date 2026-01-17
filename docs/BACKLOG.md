# GUI Unfollowed (OCR) - Detailed Engineering Backlog

This backlog is structured by milestones. Each story includes acceptance criteria and a complexity estimate.

## Complexity scale
- **XS (1)** trivial / mostly wiring
- **S (2-3)** small feature, low risk
- **M (5)** moderate feature, some integration
- **L (8)** complex multi-module work
- **XL (13)** high risk, research-heavy, or deep platform work

## Definitions
### Definition of Ready (DoR)
- Scope is bounded (clear in/out)
- Acceptance criteria are testable
- Dependencies and risks are declared

### Definition of Done (DoD)
- Acceptance criteria met
- Tests added where meaningful
- Logging for key paths and failures
- Docs updated if behavior is user-facing

---

## Milestone M0 - Repo bootstrap and governance

### US-M0-1 Repository skeleton
**Goal:** Create a commit-ready structure.
- **Acceptance Criteria**
  - Folders exist: `/src`, `/tests`, `/docs`
  - Baseline files exist: `README.md`, `.gitignore`, `.editorconfig`, `Directory.Build.props`
  - Solution structure is consistent with module docs
- **Estimate:** S (2)

### US-M0-2 Build and contribution guidelines
**Goal:** Reduce friction for contributors.
- **Acceptance Criteria**
  - `docs/BUILD.md` describes build/test steps
  - `docs/ARCHITECTURE.md` links to module boundaries and runtime flow
- **Estimate:** S (2)

### US-M0-3 CI pipeline (recommended early)
**Goal:** Prevent regressions.
- **Acceptance Criteria**
  - CI runs `dotnet restore`, `dotnet build`, `dotnet test`
  - PR checks required
- **Estimate:** M (5)

---

## Milestone M1 - Data layer: CSV import, normalization, set diff

### US-M1-1 Username normalization
**Goal:** One canonical normalization used by CSV import and OCR extraction.
- **Acceptance Criteria**
  - Trim, strip leading `@`, lower-case
  - Allow only `[a-z0-9._]`
  - Clamp length 1..30
  - Unit tests cover edge cases
- **Estimate:** S (3)

### US-M1-2 CSV import (Following / Followers)
**Goal:** Parse CSVs and extract normalized usernames with stats.
- **Acceptance Criteria**
  - Auto-detect username column from common headers (`username`, `user_name`, `handle`)
  - Fallback to manual hint/selection
  - Stats: total rows, valid usernames, invalid rows, duplicates ignored
  - Invalid rows do not fail the import
- **Estimate:** M (5)

### US-M1-3 Compute NonFollowBack
**Goal:** Compute `NonFollowBack = Following - Followers`.
- **Acceptance Criteria**
  - Correct set difference (after normalization)
  - Deterministic output (sorted list) for export/debug
  - Unit tests validate correctness
- **Estimate:** S (3)

### US-M1-4 App surface for data
**Goal:** Minimum UI/CLI to validate data pipeline quickly.
- **Acceptance Criteria**
  - User can import both CSVs
  - Totals displayed (Following / Followers / NonFollowBack)
- **Estimate:** S (2)

---

## Milestone M2 - ROI selection + overlay calibration (no OCR yet)

### US-M2-1 ROI selection UX (Region)
**Goal:** User selects a rectangular region of the screen.
- **Acceptance Criteria**
  - ROI returned as (X,Y,W,H) in screen pixel space
  - Multi-monitor supported (monitor id + coordinates)
  - User can re-select ROI
- **Estimate:** L (8)

### US-M2-2 Click-through overlay + ROI border calibration
**Goal:** Transparent always-on-top overlay aligned to ROI.
- **Acceptance Criteria**
  - Overlay does not intercept clicks/scroll
  - Overlay can draw a border exactly on ROI
  - Calibration step confirms alignment (DPI scaling accounted for)
- **Estimate:** L (8)

---

## Milestone M3 - Capture + OCR baseline (highest risk)

### US-M3-1 ROI capture engine
**Goal:** Capture frames from ROI at a controlled FPS.
- **Acceptance Criteria**
  - Stable capture loop at 3-6 FPS
  - Per-frame timing logs (capture time)
  - Works under Windows DPI scaling
- **Estimate:** XL (13)

### US-M3-2 OCR provider v0
**Goal:** OCR returns text + bounding boxes + confidence.
- **Acceptance Criteria**
  - `IOcrProvider` implementation (Windows OCR recommended first)
  - Debug mode shows recognized text and confidence
- **Estimate:** L (8)

---

## Milestone M4 - Extraction, matching, stabilization, highlights

### US-M4-1 Username extraction
**Goal:** Convert OCR tokens into handle candidates.
- **Acceptance Criteria**
  - Regex extraction: `@?[a-zA-Z0-9._]{1,30}`
  - Stopwords list supported via config
  - Normalization applied to extracted candidates
- **Estimate:** M (5)

### US-M4-2 Matching against NonFollowBackSet
**Goal:** Identify candidates that are in NonFollowBack.
- **Acceptance Criteria**
  - O(1) membership check via `HashSet`
  - No mismatches due to inconsistent normalization
- **Estimate:** S (2)

### US-M4-3 Temporal stabilizer (K-of-M)
**Goal:** Reduce highlight flicker and false positives.
- **Acceptance Criteria**
  - Defaults: K=2, M=3
  - Confidence threshold enforced
  - Unit tests verify behavior
- **Estimate:** M (5)

### US-M4-4 Render highlights
**Goal:** Draw rectangles/badges over matched usernames.
- **Acceptance Criteria**
  - Bounding boxes mapped ROI-space -> screen-space correctly
  - Overlay remains click-through
  - Updates are smooth (no major flicker)
- **Estimate:** L (8)

---

## Milestone M5 - Accuracy and performance

### US-M5-1 Preprocessing profiles
**Goal:** Improve OCR accuracy across UI themes.
- **Acceptance Criteria**
  - Profiles: Default / Light UI / Dark UI / High contrast
  - Runtime switching supported
- **Estimate:** M (5)

### US-M5-2 Adaptive OCR (frame diff gating)
**Goal:** Skip OCR if ROI is unchanged.
- **Acceptance Criteria**
  - Frame-diff threshold gates OCR execution
  - Logs show processed vs skipped frames
- **Estimate:** M (5)

### US-M5-3 Troubleshooting toolkit
**Goal:** Make field debugging possible.
- **Acceptance Criteria**
  - Toggle: show OCR text
  - Optional frame dump (1 of N frames)
  - Clear logs for DPI/coordinate issues
- **Estimate:** M (5)

---

## Milestone M6 - Packaging and hardening

### US-M6-1 Settings persistence
**Goal:** Persist settings (FPS, thresholds, ROI, theme).
- **Acceptance Criteria**
  - Settings saved and restored across runs
  - Reset-to-defaults available
- **Estimate:** M (5)

### US-M6-2 Distribution build
**Goal:** Produce a distributable artifact.
- **Acceptance Criteria**
  - Single-file publish or installer
  - Versioning and changelog
- **Estimate:** L (8)

---

## Recommended next deliverable after this backlog
To de-risk the platform work, the next logical deliverable after the skeleton is a **Windows spike** that proves:
1) click-through overlay alignment under DPI scaling, and
2) ROI capture + OCR bounding boxes that map correctly to screen coordinates.

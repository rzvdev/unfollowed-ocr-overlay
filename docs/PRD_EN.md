# docs/PRD.md

# GUI Unfollowed (OCR) — Product Requirements Document (PRD)

## 1. Context and objective

### Context
The user wants to quickly identify Instagram accounts they follow that **do not follow them back**, and to perform **manual unfollow** directly from the Instagram interface (Web or desktop app), without depending on the site’s HTML/DOM structure.

### Objective
The “GUI Unfollowed (OCR)” application assists the process of cleaning up followings by:
1) computing the “NonFollowBack” list based on two CSV files provided by the user (Following and Followers),
2) detecting in real time the usernames displayed on screen in the “Following” list using OCR,
3) highlighting accounts from “NonFollowBack” via a transparent overlay, without blocking user interaction.

### Key principle
The application **does not automate** the unfollow action (it does not click). The user decides and manually presses “Unfollow”.

---

## 2. Definitions and terms

- **Following CSV**: the list of accounts the user follows.
- **Followers CSV**: the list of accounts that follow the user.
- **NonFollowBack**: Following − Followers (after normalization).
- **OCR (Optical Character Recognition)**: text recognition from a screen capture.
- **ROI (Region of Interest)**: the screen area selected by the user (e.g., the “Following” popup) where OCR runs.
- **Overlay**: a transparent always-on-top window that draws highlights over detected elements without intercepting clicks.

---

## 3. Scope

### In-scope (MVP)
- Import two CSV files (Following + Followers).
- Normalize and compare lists to compute NonFollowBack.
- Select ROI (window or screen region).
- Real-time OCR on the ROI.
- Detect usernames and match them against NonFollowBack.
- Highlight via a click-through overlay.
- Minimal control UI: Start/Stop, select ROI, stats, OCR settings (FPS, confidence threshold).

### Out-of-scope (Non-goals)
- Auto-click / auto-unfollow / UI automation.
- Login / account management / credential access.
- Instagram API integration.
- Server-side persistence or cloud uploads (default: local-only).
- Guaranteed compatibility with all platforms (MVP target: Windows).

---

## 4. Persona & usage scenario

### Persona
An advanced user who exports CSVs from Instagram, wants efficiency, and prefers manual control over unfollow actions.

### Primary scenario
1) The user uploads `following.csv` and `followers.csv`.
2) The app generates the NonFollowBack list and shows totals.
3) The user clicks “Select ROI” and selects the “Following” popup.
4) The user clicks “Start Scanning”.
5) The user opens Instagram and scrolls through the “Following” list.
6) The app highlights NonFollowBack accounts in real time.
7) The user manually clicks “Unfollow” on highlighted accounts.

---

## 5. Functional requirements (FR)

### 5.1 CSV import & validation
- **FR-CSV-1**: The app allows uploading two `.csv` files (drag & drop and file picker).
- **FR-CSV-2**: The app automatically detects the username column based on common headers (e.g., `username`, `user_name`, `handle`).
- **FR-CSV-3**: If auto-detection fails, the user can manually select the column.
- **FR-CSV-4**: The app validates files:
  - non-empty file,
  - at least N rows (configurable, default: 1),
  - reports invalid rows (empty / missing username).
- **FR-CSV-5**: The app displays statistics: total following, total followers, total non-follow-back.

### 5.2 Normalization & matching
- **FR-MATCH-1**: Username normalization:
  - trim whitespace,
  - remove `@` prefix,
  - convert to lower-case,
  - whitelist allowed characters: `a-z`, `0-9`, `.`, `_`.
- **FR-MATCH-2**: The app computes `NonFollowBack = FollowingSet - FollowersSet`.
- **FR-MATCH-3**: The app keeps the list in memory as a set (O(1) membership checks).

### 5.3 ROI selection & real-time OCR
- **FR-OCR-1**: The user can select:
  - a window (window capture), or
  - a rectangular region (region capture).
- **FR-OCR-2**: The app provides an ROI preview before starting scanning.
- **FR-OCR-3**: OCR runs in a loop with configurable FPS (default: 4 FPS).
- **FR-OCR-4**: OCR returns text + bounding boxes + confidence per token/line (if supported by the engine).
- **FR-OCR-5**: Extract candidate usernames using:
  - handle regex: `@?[a-zA-Z0-9._]{1,30}`,
  - whitelist filtering,
  - stopword exclusion (e.g., “Following”, “Follow”, “Message”, “Remove”, etc.).

### 5.4 Highlight overlay (click-through)
- **FR-UI-1**: Always-on-top, transparent overlay.
- **FR-UI-2**: The overlay is click-through (does not intercept click/scroll/hover).
- **FR-UI-3**: For each detected username with a bounding box:
  - if ∈ NonFollowBack → highlight (outline + “No Follow Back” badge),
  - otherwise → no highlight.
- **FR-UI-4**: Highlights are stable (no excessive flicker) using temporal stabilization.

### 5.5 Stabilization and error reduction (mandatory for UX)
- **FR-STAB-1**: A username is considered “validly detected” only if it appears in at least K of the last M frames (default: K=2, M=3).
- **FR-STAB-2**: If OCR confidence < threshold (default: 0.60), mark as “uncertain” or do not highlight (configurable).
- **FR-STAB-3**: Exclude duplicates per frame and deduplicate across the visible list.

### 5.6 Export (optional for MVP, recommended for v1)
- **FR-EXP-1**: Export NonFollowBack to CSV/JSON.
- **FR-EXP-2**: Copy-to-clipboard for a username selected from the list.

---

## 6. Non-functional requirements (NFR)

- **NFR-1 Performance**: On a typical ROI (following popup), the app runs smoothly without blocking the UI; target: < 50% CPU on a mid-range system, with FPS=4.
- **NFR-2 Latency**: Highlight updates in < 250–400 ms perceived (depending on OCR engine).
- **NFR-3 DPI robustness**: Support Windows scaling (100%–200%); correct mapping of OCR coordinates → screen.
- **NFR-4 Local-only**: CSV files and processing remain local; no implicit upload.
- **NFR-5 Observability**: Local logs, debug mode with optional “frame dump”.
- **NFR-6 Safety**: No credential collection, no code injection into apps, no automated clicking.

---

## 7. Constraints and dependencies

- OCR depends on the quality of on-screen text (font, contrast, zoom).
- DPI scaling and multi-monitor setups can affect overlay coordinates.
- Instagram UI may change; OCR is more robust than DOM parsing but can be affected by font/contrast.

---

## 8. Operating guide (for the user)

- Recommended Instagram zoom: 125%–150% for stable OCR (configurable).
- Select the ROI strictly around the “Following” list (excluding header/footer) for performance.
- Choose a suitable preprocessing profile (Light/Dark).
- Scan at a moderate FPS (3–6), not maximum.

---

## 9. User stories & acceptance criteria

### US-1 Import CSV
As a user, I want to upload two CSVs (Following and Followers) to obtain the NonFollowBack list.
- **AC-1**: The app computes totals and NonFollowBack correctly.
- **AC-2**: Invalid rows are reported separately.

### US-2 Select ROI
As a user, I want to select the on-screen area containing the “Following” list so OCR runs only there.
- **AC-1**: ROI can be selected and previewed.
- **AC-2**: ROI persists for the session (at minimum).

### US-3 Highlight
As a user, I want to see highlights over accounts that don’t follow back so I can identify them instantly.
- **AC-1**: The overlay does not block clicks.
- **AC-2**: Highlight remains stable while scrolling and does not “pulse” excessively.

### US-4 Control scanning
As a user, I want Start/Stop scanning so I can control processing.
- **AC-1**: Stop halts OCR and the overlay.
- **AC-2**: Start resumes without restarting the application.

---

## 10. “Definition of Done” criteria (MVP)

The MVP is “Done” if:
1) CSV import + matching produces a correct and repeatable NonFollowBack list.
2) ROI selection works and preview confirms the area.
3) OCR detects usernames in the ROI with sufficiently consistent bounding boxes.
4) The click-through overlay highlights NonFollowBack accounts in real time.
5) Temporal stabilization reduces flicker to an acceptable level.
6) The app runs local-only and provides basic logs.

---

## 11. Roadmap

### MVP
- CSV import + NonFollowBack
- ROI selection
- OCR pipeline + highlight overlay
- Stabilization + minimal settings

### v1
- Export NonFollowBack
- Preprocessing profiles (Light/Dark/High contrast)
- Adaptive FPS (frame-diff)
- “Seen list” (progress: how many non-follow-back accounts have been detected in ROI)
- Troubleshooting UI (show live OCR text)

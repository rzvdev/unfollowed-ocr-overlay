# unfollowed-ocr-overlay

**Windows .NET 8 desktop application that uses real-time OCR and a transparent screen overlay to highlight non-follow-back accounts from CSV exports.**  
Local-only execution. No browser extensions. No automation. No credentials required.

![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![OCR](https://img.shields.io/badge/OCR-Real_Time-0A66C2)
![Execution](https://img.shields.io/badge/Local_Only-Yes-E5533D)
![License](https://img.shields.io/badge/MIT-License-16A34A)

---

## Overview

`unfollowed-ocr-overlay` is a desktop utility designed to **visually assist manual unfollow actions** on social platforms by overlaying OCR-based highlights directly on the screen.

Instead of coupling logic to website HTML (fragile, anti-bot sensitive), the application operates at the **visual layer**, using real-time OCR to detect usernames rendered on screen and compare them against a precomputed dataset (CSV).

The user remains fully in control at all times.

---

## Key Principles

- No browser extensions  
- No DOM scraping  
- No automated actions  
- No login or credentials  
- Local-only processing  
- Human-in-the-loop  

---

## Core Features

- Real-time OCR over a selected screen region  
- Transparent, click-through overlay window  
- Highlights usernames that meet configurable conditions (e.g. non-follow-back)  
- CSV-driven comparison logic  
- Platform-agnostic (works with any UI that visually renders usernames)  
- Hotkey-controlled start / pause / stop  
- Debug & diagnostics mode (OCR bounding boxes, confidence levels)  

---

## Typical Use Case

1. Export followers / following data to CSV  
2. Preprocess and compute the **non-follow-back set**  
3. Open the target app or website normally  
4. Start the overlay  
5. Scroll naturally  
6. Non-follow-back usernames are visually highlighted  
7. Manually unfollow at your discretion  

---

## High-Level Architecture

```
+-------------------+
|   CSV Input       |
+-------------------+
          |
          v
+-------------------+
| Comparison Engine |
+-------------------+
          |
          v
+-------------------+       +--------------------+
| OCR Engine        | --->  | Overlay Renderer   |
| (Screen Capture)  |       | (Click-through UI)|
+-------------------+       +--------------------+
```

---

## Technology Stack

- .NET 8 (Windows)  
- WPF (overlay & UI)  
- Real-time OCR engine (pluggable)  
- Dependency Injection  
- Clean Architecture (Core / Application / Infrastructure / UI)  
- Strong separation between OCR, domain logic, and rendering  

---

## Project Structure (Simplified)

```
/src
  /Unfollowed.App
  /Unfollowed.Capture
  /Unfollowed.Core
  /Unfollowed.Csv
  /Unfollowed.Ocr
  /Unfollowed.Overlay
  /Unfollowed.Overlay.Win32
  /Unfolloweed.Preprocess
  /tests
/tests
/docs
```

---

## Configuration

Runtime configuration is defined in `src/Unfollowed.App/appsettings.json`. See the configuration reference in
`docs/CONFIGURATION.md` for details on each option.

## Non-Goals (Explicitly Out of Scope)

- Automated unfollowing  
- Browser automation  
- Headless scraping  
- Account login handling  
- Bypassing platform safeguards  

This tool is **assistive**, not autonomous.

---

## Compliance & Ethics

This project is designed to:
- Keep the user in full control  
- Avoid automated interactions  
- Reduce detection risk  
- Respect platform boundaries by operating visually, not programmatically  

Usage remains the responsibility of the end user.

---

## Development Status

- Architecture: defined  
- Interfaces: defined  
- Backlog: in progress  
- Initial skeleton: planned  
- OCR engine selection: configurable  

---

## License

**MIT License**  
See `LICENSE` for details.

---

## Disclaimer

This project is provided **as-is**, for educational and productivity purposes.  
The author assumes no responsibility for misuse or violation of third-party terms.

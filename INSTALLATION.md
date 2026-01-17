# Installation

## System Requirements

- Windows 10 or Windows 11 (64-bit)
- .NET 8 SDK
- GPU not required (CPU OCR supported)
- Minimum screen resolution: 1920x1080 recommended

---

## Prerequisites

1. Install **.NET 8 SDK**  
   https://dotnet.microsoft.com/download/dotnet/8.0

2. (Optional) Visual Studio 2022  
   - Workloads:
     - .NET Desktop Development
   - Recommended for debugging and UI work

---

## Installation Steps

### Option A: Using Prebuilt Binary (Future)

> Not yet available.  
> This project is currently source-based.

---

### Option B: From Source (Recommended)

1. Clone the repository:
   ```bash
   git clone https://github.com/<your-org>/unfollowed-ocr-overlay.git
   ```

2. Navigate to the project root:
   ```bash
   cd unfollowed-ocr-overlay
   ```

3. Restore dependencies:
   ```bash
   dotnet restore
   ```

---

## First Run Checklist

- Ensure DPI scaling is set to **100% or 125%** (recommended)
- Run as **normal user** (admin not required)
- Close other screen-capture-heavy applications

---

## Permissions

- Screen capture access (Windows)
- No network permissions required
- No elevated privileges required

---

## Uninstallation

Simply delete the project folder or compiled binaries.  
No registry entries or system-wide changes are made.

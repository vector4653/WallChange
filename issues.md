# WallChange - Issues Tracker

A consolidated log of known bugs, technical limitations, and feature requests. All issues are tracked with reproduction conditions, root-cause analyses, and architectural remediation plans.

---

## Issue Registry

| ID | Title | Status | Severity | Target Module |
|:---|:---|:---:|:---:|:---|
| [WC-ISS-001](#wc-iss-001-multi-monitor--virtual-desktop-desktop-detection-failure) | Desktop detection fails when windows are open on secondary screens or virtual desktops | Resolved | High | `DesktopDetector`, `MainOverlayWindow` |
| [WC-ISS-002](#wc-iss-002-selected-wallpaper-folder-not-persisted-across-sessions) | Selected wallpaper folder is not persisted across application restarts | Resolved | Medium | `SettingsManager`, `MainOverlayWindow` |

---

## Detailed Issue Reports

### [WC-ISS-001]: Multi-Monitor & Virtual Desktop Desktop Detection Failure

- **Status:** Resolved
- **Severity:** High
- **Date Reported:** 2026-10-08
- **Date Resolved:** 2026-10-08
- **Affected Files:**
  - [`Core/DesktopDetector.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Core/DesktopDetector.cs)
  - [`UI/MainOverlayWindow.xaml.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/UI/MainOverlayWindow.xaml.cs)
  - [`Tests/WallChange.Tests/DesktopDetectorTests.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Tests/WallChange.Tests/DesktopDetectorTests.cs)

#### 1. Problem Description
On multi-monitor setups or environments with multiple Windows 11 Virtual Desktops, the WallChange edge hover trigger does not activate on the primary monitor when that monitor is showing an unobstructed desktop, provided that an application window is open/focused on a secondary display or another virtual desktop.

#### 2. Root Cause Analysis
1. **Global Foreground Window Dependency:**
   In [`DesktopDetector.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Core/DesktopDetector.cs#L24-L33), desktop state was checked via `GetForegroundWindow()`. This Win32 API returns a single system-wide foreground window. If an application window on Monitor 2 had focus, `GetForegroundWindow()` returned that window's HWND, causing `IsDesktopWindow()` to return `false`. As a result, [`MainOverlayWindow.xaml.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/UI/MainOverlayWindow.xaml.cs#L232) aborted the hover trigger on Monitor 1 despite the desktop being completely visible and unoccupied.
2. **Virtual Desktop Cloaking (`DWMWA_CLOAKED`):**
   Windows 11 cloaks windows residing on inactive virtual desktops rather than minimizing or hiding them. Inactive desktop windows retain visibility styles, and focus transitions across virtual desktops caused stale foreground handle reporting because cloaked windows were not filtered out using `DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, ...)`.
3. **Fixed Primary Screen Metrics:**
   Screen edge collision evaluated bounds against `GetSystemMetrics(SM_CXSCREEN)` and `SystemParameters.PrimaryScreenWidth`, which did not account for multi-monitor desktop coordinate offsets.

#### 3. Steps to Reproduce
1. Connect two displays to the machine.
2. Focus an application (e.g., browser or editor) on the secondary monitor.
3. Move the cursor to the docking edge on the primary display where only the desktop background is visible.
4. Hover along the trigger zone.
5. **Observed:** The overlay remains hidden and will not slide in until the user clicks the primary desktop surface to steal focus.

#### 4. Resolution
1. **Per-Monitor Focus Differentiation:**
   In [`DesktopDetector.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Core/DesktopDetector.cs), `IsDesktopActiveForWindow()` queries the monitor of both the foreground window and the WallChange overlay via `MonitorFromWindow`. When the active application resides on a secondary display, it no longer marks the primary display's desktop as inactive.
2. **DWM Cloaking & Iconic Filtering:**
   Added `IsWindowCloaked` using `DwmGetWindowAttribute` with `DWMWA_CLOAKED (14)` and `IsWindowIconic` via `IsIconic()`. Inactive virtual desktop windows and minimized windows no longer block hover activation.
3. **Point-Based Surface Hit-Testing:**
   Implemented `IsDesktopActiveForWindowOrPoint()` using `WindowFromPoint` and `GetAncestor(GA_ROOT)` to verify that the specific screen edge point is directly over desktop background / taskbar / shell islands rather than occluded by an application window on the same monitor.
4. **Multi-Monitor Bounds Resolution:**
   Updated [`UpdateWindowPositionAndBounds`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/UI/MainOverlayWindow.xaml.cs) and [`EdgeHoverMonitor_Tick`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/UI/MainOverlayWindow.xaml.cs) to resolve coordinates against `GetMonitorInfo(MonitorFromWindow(...))` with accurate per-monitor DPI scaling.
5. **Dynamic Active Monitor Following:**
   Resolved interaction monitor directly from cursor coordinates (`MonitorFromPoint(pt)`). When the user moves the cursor to the dock zone of *any* connected display (main or secondary), WallChange dynamically positions itself on that display and activates if that display's desktop is visible.

---

### [WC-ISS-002]: Selected Wallpaper Folder Not Persisted Across Sessions

- **Status:** Resolved
- **Severity:** Medium
- **Date Reported:** 2026-10-08
- **Date Resolved:** 2026-10-08
- **Affected Files:**
  - [`Core/AppSettings.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Core/AppSettings.cs)
  - [`Core/SettingsManager.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Core/SettingsManager.cs)
  - [`UI/MainOverlayWindow.xaml.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/UI/MainOverlayWindow.xaml.cs)
  - [`Tests/WallChange.Tests/SettingsManagerTests.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Tests/WallChange.Tests/SettingsManagerTests.cs)

#### 1. Problem Description
When a user customizes their wallpaper collection by browsing and selecting a specific directory (via the `Browse Folder` button), the chosen folder path was only held in runtime memory. Upon quitting and relaunching WallChange, the application forgot the user's selected folder and reset to the default fallback directory (`C:\Windows\Web\Wallpaper`), requiring the user to re-browse every time the application started.

#### 2. Root Cause Analysis
1. **Lack of Configuration Persistence Layer:**
   The application did not implement a persistent configuration store.
2. **Hardcoded Fallback Initialization on Launch:**
   `Window_Loaded` directly invoked `TryLoadDefaultWindowsWallpaper()`, which unconditionally loaded images from `C:\Windows\Web\Wallpaper`.
3. **No Save Trigger in Folder Picker:**
   In `BtnBrowse_Click`, when `dialog.FolderName` was loaded into `_wallpaperManager.LoadFolder()`, no serialization logic was executed to persist the path.

#### 3. Steps to Reproduce
1. Launch WallChange.
2. Click **Browse Folder** and select any custom folder containing images (e.g., `C:\Users\...\Pictures\Wallpapers`).
3. Notice that the wallpapers load and display correctly for the session.
4. Exit the application completely and relaunch it.
5. **Observed:** The application resets back to `C:\Windows\Web\Wallpaper` with the default Windows wallpapers rather than resuming from the user's chosen folder.

#### 4. Resolution
1. **Created `AppSettings` & `SettingsManager` in `WallChange.Core`:**
   Implemented a JSON-backed settings manager stored at `%LocalAppData%\WallChange\settings.json`. It stores:
   - `WallpaperDirectory`: Last selected folder path
   - `LastWallpaperIndex`: Last viewed/applied wallpaper index
   - `LastWallpaperPath`: Last applied wallpaper image path
   - `DockEdge`: Selected dock position edge
   - `IsPinned`: Pinned state
   - `WallpaperStyle`: Desktop wallpaper fit/fill mode
2. **Lifecycle Integration:**
   - **On Launch (`Window_Loaded`):** Calls `LoadInitialWallpapers()`. If a valid saved directory exists, it loads that folder and restores the last wallpaper index; if absent or corrupted, falls back gracefully to `TryLoadDefaultWindowsWallpaper()`.
   - **On Folder Selection (`BtnBrowse_Click`):** Invokes `_settingsManager.UpdateWallpaperDirectory(...)`.
   - **On Wallpaper Advance / Shuffle (`OnWallpaperChanged`):** Automatically saves the current image index and path.
   - **On Edge / Pin Changes:** Automatically synchronizes edge docking and pin state with `settings.json`.
3. **Automated Unit Tests:**
   Created [`SettingsManagerTests.cs`](file:///C:/Users/ritvi/Downloads/Apps/WallChange/Tests/WallChange.Tests/SettingsManagerTests.cs) verifying default creation, save/load roundtrips, directory updates, and graceful fallback on corrupted JSON.

---

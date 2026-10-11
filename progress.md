# WallChange - Windows 11 Wallpaper Changer

## Project Overview
WallChange is a native Windows 11 wallpaper changer application featuring smooth animated transitions, desktop-only hover activation, edge snapping, and a modern Fluent/Mica UI.

---

> [!NOTE]
> **Current Progress Saved:**
> Converted UI to a clean, frosted grey-white Fluent aesthetic with matching slate typography and unified button styling (removed blue accents). Fixed the non-hovering outline artifact by removing DWM non-client border preferences on the transparent layered window and ensuring the window is fully hidden (`Visibility = Visibility.Hidden`) when docked. Verified with 26/26 passing tests.
>
> **Focus for Next Session:**
> Conduct further hands-on debugging and fine UI polish (spacing, interaction feedback, edge transitions, and visual refinements) before moving on to Phase 4 (Wallpaper Animation Engine).

---

## Phased Development Progress

### [x] Phase 1: Project Setup & Core Logic
- [x] Initialize project with recommended framework (C# .NET WPF with modern Win32 / DWM integration)
- [x] Implement directory scanning to discover supported image formats (`.jpg`, `.jpeg`, `.png`, `.bmp`, `.webp`)
- [x] Implement core Windows API wallpaper changer (`SystemParametersInfo` / `IDesktopWallpaper`)
- [x] Automated unit test suite verifying directory scanning, bounds handling, cycling, and API failure modes
- [x] *Status: Completed - Awaiting user approval to proceed to Phase 2*

---

### [x] Phase 2: UI Foundation & Edge Snapping
- [x] Design modern Windows 11 panel (Mica/Acrylic backdrop, rounded corners, Fluent icons, DropShadow)
- [x] Implement screen edge anchoring logic (Top-Middle, Bottom-Middle, Left-Middle, Right-Middle)
- [x] Implement mouse hover detection zones & smooth slide-in / slide-out animations with cubic easing
- [x] Multi-monitor DPI-aware bounds calculation and toolwindow style (`WS_EX_TOOLWINDOW`)
- [x] *Status: Completed - Awaiting user approval to proceed to Phase 3*

---

### [x] Phase 3: Desktop Visibility Detection
- [x] Implement Win32 API hooks & foreground window tracking (`GetForegroundWindow`, `GetShellWindow`, `SetWinEventHook` with `EVENT_SYSTEM_FOREGROUND`)
- [x] Distinguish Desktop (WorkerW / Progman / SHELLDLL_DefView) focus from active application windows
- [x] Integrate desktop focus check into hover activation trigger (UI only slides in when desktop is active and unobstructed, or pinned)
- [x] Auto slide-out when focus shifts from desktop to another application window
- [x] Automated unit test suite verifying window handle validation and detector lifecycle
- [x] *Status: Completed - Awaiting user approval to proceed to Phase 4*

---

### [ ] Phase 4: Animation Engine
- [ ] Research & implement desktop wallpaper layering mechanism (parenting borderless transition window behind desktop icons via `WorkerW` / `Progman` hook `0x052C`)
- [ ] Implement Transition Style 1: Smooth Crossfade
- [ ] Implement Transition Style 2: Directional Slide / Zoom Fade
- [ ] Synchronize visual transition completion with OS wallpaper commit (`SystemParametersInfo` / `IDesktopWallpaper`)
- [ ] *Status: Pending Phase 3 completion & approval*

---

### [ ] Phase 5: Settings & Polish
- [ ] Connect UI controls to core logic (Next, Previous, Play/Pause, Randomize)
- [ ] Add wallpaper scaling options (Fill, Fit, Stretch, Center, Span)
- [x] Implement persistent configuration system (JSON config for selected folder, edge position, pin state, wallpaper style)
- [ ] Background timer service for automated periodic wallpaper switching
- [ ] Final end-to-end testing, error handling, and performance optimization
- [ ] *Status: Pending Phase 4 completion & approval*

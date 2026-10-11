using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WallChange.Core;

public class DesktopDetector : IDisposable
{
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hWnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    private const uint GA_PARENT = 1;
    private const uint GA_ROOT = 2;
    private const uint GA_ROOTOWNER = 3;

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    public const uint MONITOR_DEFAULTTONULL = 0;
    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    public const int DWMWA_CLOAKED = 14;

    private readonly WinEventDelegate _winEventProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private IntPtr _ownWindowHandle = IntPtr.Zero;
    private bool _isDesktopActive = false;
    private bool _disposed = false;

    public event EventHandler<bool>? DesktopActiveChanged;

    public bool IsDesktopActive => _isDesktopActive;

    public DesktopDetector()
    {
        _winEventProc = new WinEventDelegate(OnWinEvent);
    }

    /// <summary>
    /// Starts monitoring foreground window changes.
    /// </summary>
    public void Start(IntPtr ownWindowHandle)
    {
        _ownWindowHandle = ownWindowHandle;

        // Perform initial check
        UpdateForegroundState(GetForegroundWindow());

        // Hook foreground window changes
        try
        {
            _hookHandle = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set WinEventHook: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops monitoring foreground window changes.
    /// </summary>
    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWinEvent(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hWnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (eventType == EVENT_SYSTEM_FOREGROUND)
        {
            UpdateForegroundState(hWnd);
        }
    }

    /// <summary>
    /// Re-evaluates if the currently focused window or monitor state indicates the desktop is active.
    /// </summary>
    public bool CheckCurrentState()
    {
        IntPtr fg = GetForegroundWindow();
        UpdateForegroundState(fg);
        return _isDesktopActive;
    }

    private void UpdateForegroundState(IntPtr fg)
    {
        bool newDesktopState = IsDesktopActiveForWindow(fg, _ownWindowHandle);

        if (newDesktopState != _isDesktopActive)
        {
            _isDesktopActive = newDesktopState;
            DesktopActiveChanged?.Invoke(this, _isDesktopActive);
        }
    }

    /// <summary>
    /// Checks whether a window is cloaked by DWM (e.g., residing on an inactive Windows Virtual Desktop).
    /// </summary>
    public bool IsWindowCloaked(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            int hr = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
            return hr == 0 && cloaked != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether a window is minimized (iconic).
    /// </summary>
    public bool IsWindowIconic(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return false;
        try
        {
            return IsIconic(hWnd);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the desktop on the target monitor is active,
    /// taking into account cloaked virtual desktop windows, minimized windows,
    /// and windows on other monitors.
    /// </summary>
    public bool IsDesktopActiveForMonitor(IntPtr fg, IntPtr targetMonitor)
    {
        // When no application window is focused, desktop is active
        if (fg == IntPtr.Zero) return true;

        // If foreground is desktop, taskbar, shell or our own window
        if (IsDesktopWindow(fg)) return true;

        // If foreground window is cloaked (on another virtual desktop), desktop is unobstructed
        if (IsWindowCloaked(fg)) return true;

        // If foreground window is minimized, desktop is unobstructed
        if (IsWindowIconic(fg)) return true;

        // Check if foreground window is on a different monitor
        if (targetMonitor != IntPtr.Zero)
        {
            try
            {
                IntPtr fgMon = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
                if (fgMon != IntPtr.Zero && fgMon != targetMonitor)
                {
                    // Foreground window is on another monitor; target monitor has no active foreground window
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Monitor check failed: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Checks whether the desktop on the target window's monitor is active.
    /// </summary>
    public bool IsDesktopActiveForWindow(IntPtr fg, IntPtr targetWindow)
    {
        IntPtr targetMon = targetWindow != IntPtr.Zero
            ? MonitorFromWindow(targetWindow, MONITOR_DEFAULTTONEAREST)
            : IntPtr.Zero;
        return IsDesktopActiveForMonitor(fg, targetMon);
    }

    /// <summary>
    /// Checks if the desktop is active on the monitor hosting the given screen point,
    /// and verifies that the screen point is over desktop wallpaper / taskbar / empty space.
    /// </summary>
    public bool IsDesktopActiveForPoint(POINT pt, int inwardDeltaX = 0, int inwardDeltaY = 0)
    {
        IntPtr fg = GetForegroundWindow();
        IntPtr targetMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

        if (IsDesktopActiveForMonitor(fg, targetMon))
        {
            // Verify that the specific screen edge point is not occluded by an inactive window on this display
            if (IsPointOnDesktopSurface(pt, _ownWindowHandle))
            {
                return true;
            }

            // Check inward offset point (to handle 1-pixel window border / maximized margins)
            if (inwardDeltaX != 0 || inwardDeltaY != 0)
            {
                POINT inwardPt = new POINT(pt.X + inwardDeltaX, pt.Y + inwardDeltaY);
                if (IsPointOnDesktopSurface(inwardPt, _ownWindowHandle))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the desktop is active for a window or point.
    /// </summary>
    public bool IsDesktopActiveForWindowOrPoint(IntPtr windowHandle, POINT pt, int inwardDeltaX = 0, int inwardDeltaY = 0)
    {
        return IsDesktopActiveForPoint(pt, inwardDeltaX, inwardDeltaY);
    }

    private bool IsPointOnDesktopSurface(POINT pt, IntPtr windowHandle)
    {
        IntPtr hwndAtPt = WindowFromPoint(pt);
        if (hwndAtPt == IntPtr.Zero) return true;

        if (windowHandle != IntPtr.Zero && hwndAtPt == windowHandle) return true;

        IntPtr root = GetAncestor(hwndAtPt, GA_ROOT);
        if (root == IntPtr.Zero) root = hwndAtPt;

        if (windowHandle != IntPtr.Zero && root == windowHandle) return true;

        if (IsDesktopWindow(root) || IsDesktopWindow(hwndAtPt))
        {
            return true;
        }

        // If window under cursor is cloaked or minimized, treat as desktop surface
        if (IsWindowCloaked(root) || IsWindowIconic(root))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tests if a given window handle represents the Windows Desktop.
    /// </summary>
    public bool IsDesktopWindow(IntPtr hWnd)
    {
        // When no application window is focused, desktop is the active foreground
        if (hWnd == IntPtr.Zero) return true;

        // If it's our own overlay window, preserve active state
        if (_ownWindowHandle != IntPtr.Zero && hWnd == _ownWindowHandle)
        {
            return true;
        }

        IntPtr shell = GetShellWindow();
        if (hWnd == shell) return true;

        IntPtr desktop = GetDesktopWindow();
        if (hWnd == desktop) return true;

        // Check window class
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        string className = sb.ToString();

        // Standard desktop, WorkerW, and Windows 11 shell containers
        if (className.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("SysListView32", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

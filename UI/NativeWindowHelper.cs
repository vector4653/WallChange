using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WallChange.UI;

public static class NativeWindowHelper
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// Configures the window as a tool window so it does not appear in Alt+Tab.
    /// Does NOT set DWM window corner preferences because this is a transparent layered window (AllowsTransparency="True")
    /// which renders custom rounded corners via WPF. Setting DWM corner preferences on layered windows
    /// causes Windows 11 DWM to paint a persistent 1px system border/outline around the HWND bounding box.
    /// </summary>
    public static void SetToolWindow(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var handle = helper.EnsureHandle();
        int currentExStyle = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, currentExStyle | WS_EX_TOOLWINDOW);
    }
}

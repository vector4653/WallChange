using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WallChange.Core;

public static class WallpaperNative
{
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        string pvParam,
        uint fWinIni);

    // COM Interface IDesktopWallpaper for modern Windows (Windows 8+)
    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaperCoClass
    {
    }

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);
        [return: MarshalAs(UnmanagedType.U4)]
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out NativeRect displayRect);
        void SetBackgroundColor([MarshalAs(UnmanagedType.U4)] uint color);
        [return: MarshalAs(UnmanagedType.U4)]
        uint GetBackgroundColor();
        void SetPosition([MarshalAs(UnmanagedType.I4)] DesktopWallpaperPosition position);
        [return: MarshalAs(UnmanagedType.I4)]
        DesktopWallpaperPosition GetPosition();
        void SetSlideshow(IntPtr items);
        IntPtr GetSlideshow();
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.I4)] int direction);
        int GetStatus();
        bool Enable();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public enum DesktopWallpaperPosition
    {
        Center = 0,
        Tile = 1,
        Stretch = 2,
        Fit = 3,
        Fill = 4,
        Span = 5
    }

    /// <summary>
    /// Sets the desktop wallpaper using IDesktopWallpaper (with fallback to SystemParametersInfo).
    /// </summary>
    public static bool SetWallpaper(string imagePath, WallpaperStyle style = WallpaperStyle.Fill)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            throw new FileNotFoundException("Wallpaper image file not found", imagePath);
        }

        string fullPath = Path.GetFullPath(imagePath);

        // Try modern IDesktopWallpaper COM interface first
        try
        {
            var wallpaperObj = (IDesktopWallpaper)new DesktopWallpaperCoClass();
            var pos = style switch
            {
                WallpaperStyle.Center => DesktopWallpaperPosition.Center,
                WallpaperStyle.Tile => DesktopWallpaperPosition.Tile,
                WallpaperStyle.Stretch => DesktopWallpaperPosition.Stretch,
                WallpaperStyle.Fit => DesktopWallpaperPosition.Fit,
                WallpaperStyle.Fill => DesktopWallpaperPosition.Fill,
                WallpaperStyle.Span => DesktopWallpaperPosition.Span,
                _ => DesktopWallpaperPosition.Fill
            };

            wallpaperObj.SetPosition(pos);
            wallpaperObj.SetWallpaper(null, fullPath);
            return true;
        }
        catch
        {
            // Fallback to legacy SystemParametersInfo + Registry
            return SetWallpaperLegacy(fullPath, style);
        }
    }

    private static bool SetWallpaperLegacy(string fullPath, WallpaperStyle style)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
            if (key != null)
            {
                var (styleVal, tileVal) = style switch
                {
                    WallpaperStyle.Fill => ("10", "0"),
                    WallpaperStyle.Fit => ("6", "0"),
                    WallpaperStyle.Stretch => ("2", "0"),
                    WallpaperStyle.Tile => ("0", "1"),
                    WallpaperStyle.Center => ("0", "0"),
                    WallpaperStyle.Span => ("22", "0"),
                    _ => ("10", "0")
                };

                key.SetValue("WallpaperStyle", styleVal);
                key.SetValue("TileWallpaper", tileVal);
            }

            return SystemParametersInfo(
                SPI_SETDESKWALLPAPER,
                0,
                fullPath,
                SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
        catch
        {
            return false;
        }
    }
}

namespace WallChange.Core;

public enum AppThemeMode
{
    Light = 0,
    Dark = 1,
    Adaptive = 2
}

public class AppSettings
{
    public string? WallpaperDirectory { get; set; }
    public int LastWallpaperIndex { get; set; } = 0;
    public string? LastWallpaperPath { get; set; }
    public string DockEdge { get; set; } = "TopMiddle";
    public bool IsPinned { get; set; } = false;
    public WallpaperStyle WallpaperStyle { get; set; } = WallpaperStyle.Fill;
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Light;
}

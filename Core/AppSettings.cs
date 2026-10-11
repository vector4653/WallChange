namespace WallChange.Core;

public class AppSettings
{
    public string? WallpaperDirectory { get; set; }
    public int LastWallpaperIndex { get; set; } = 0;
    public string? LastWallpaperPath { get; set; }
    public string DockEdge { get; set; } = "TopMiddle";
    public bool IsPinned { get; set; } = false;
    public WallpaperStyle WallpaperStyle { get; set; } = WallpaperStyle.Fill;
}

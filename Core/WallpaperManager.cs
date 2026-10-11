using System.IO;
using System.Linq;

namespace WallChange.Core;

public class WallpaperChangedEventArgs : EventArgs
{
    public string ImagePath { get; }
    public int Index { get; }
    public int TotalCount { get; }

    public WallpaperChangedEventArgs(string imagePath, int index, int totalCount)
    {
        ImagePath = imagePath;
        Index = index;
        TotalCount = totalCount;
    }
}

public class WallpaperManager
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jfif", ".tiff", ".tif"
    };

    private readonly List<string> _images = new();
    private readonly Random _random = new();
    private int _currentIndex = -1;
    private string? _currentFolderPath;
    private WallpaperStyle _currentStyle = WallpaperStyle.Fill;

    public event EventHandler<WallpaperChangedEventArgs>? WallpaperChanged;
    public event EventHandler? FolderLoaded;

    public IReadOnlyList<string> Images => _images.AsReadOnly();
    public int CurrentIndex => _currentIndex;
    public int TotalCount => _images.Count;
    public string? CurrentFolderPath => _currentFolderPath;
    public WallpaperStyle CurrentStyle
    {
        get => _currentStyle;
        set => _currentStyle = value;
    }

    public string? CurrentImagePath =>
        (_currentIndex >= 0 && _currentIndex < _images.Count) ? _images[_currentIndex] : null;

    /// <summary>
    /// Loads image files from the specified folder.
    /// </summary>
    public int LoadFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        _currentFolderPath = Path.GetFullPath(folderPath);
        _images.Clear();

        try
        {
            var files = Directory.EnumerateFiles(_currentFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _images.AddRange(files);
            _currentIndex = _images.Count > 0 ? 0 : -1;
        }
        catch (Exception)
        {
            _currentIndex = -1;
            throw;
        }

        FolderLoaded?.Invoke(this, EventArgs.Empty);
        return _images.Count;
    }

    /// <summary>
    /// Navigates to the next wallpaper and applies it.
    /// </summary>
    public bool NextWallpaper()
    {
        if (_images.Count == 0) return false;

        _currentIndex = (_currentIndex + 1) % _images.Count;
        return ApplyCurrent();
    }

    /// <summary>
    /// Navigates to the previous wallpaper and applies it.
    /// </summary>
    public bool PreviousWallpaper()
    {
        if (_images.Count == 0) return false;

        _currentIndex = (_currentIndex - 1 + _images.Count) % _images.Count;
        return ApplyCurrent();
    }

    /// <summary>
    /// Selects a random wallpaper and applies it.
    /// </summary>
    public bool RandomWallpaper()
    {
        if (_images.Count == 0) return false;
        if (_images.Count == 1) return ApplyCurrent();

        int newIndex;
        do
        {
            newIndex = _random.Next(0, _images.Count);
        } while (newIndex == _currentIndex && _images.Count > 1);

        _currentIndex = newIndex;
        return ApplyCurrent();
    }

    /// <summary>
    /// Applies a specific wallpaper by index.
    /// </summary>
    public bool SetWallpaperByIndex(int index)
    {
        if (index < 0 || index >= _images.Count) return false;

        _currentIndex = index;
        return ApplyCurrent();
    }

    /// <summary>
    /// Applies the current wallpaper to the desktop.
    /// </summary>
    public bool ApplyCurrent()
    {
        var path = CurrentImagePath;
        if (string.IsNullOrEmpty(path)) return false;

        bool success = WallpaperNative.SetWallpaper(path, _currentStyle);
        if (success)
        {
            WallpaperChanged?.Invoke(this, new WallpaperChangedEventArgs(path, _currentIndex, _images.Count));
        }

        return success;
    }
}

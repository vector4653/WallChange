using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using WallChange.Core;
using Xunit;

namespace WallChange.Tests;

public class WallpaperManagerTests : IDisposable
{
    private readonly string _tempDir;

    public WallpaperManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WallChange_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    private string CreateDummyImage(string filename, int width = 100, int height = 100)
    {
        var filePath = Path.Combine(_tempDir, filename);
        using (var bmp = new Bitmap(width, height))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Blue);
            bmp.Save(filePath, ImageFormat.Png);
        }
        return filePath;
    }

    [Fact]
    public void LoadFolder_NonExistent_ThrowsDirectoryNotFound()
    {
        var manager = new WallpaperManager();
        Assert.Throws<DirectoryNotFoundException>(() => manager.LoadFolder(Path.Combine(_tempDir, "DoesNotExist")));
    }

    [Fact]
    public void LoadFolder_FiltersExtensionsCorrectly()
    {
        CreateDummyImage("img1.png");
        CreateDummyImage("img2.jpg");
        CreateDummyImage("img3.bmp");
        File.WriteAllText(Path.Combine(_tempDir, "document.txt"), "hello");
        File.WriteAllText(Path.Combine(_tempDir, "video.mp4"), "fake video");

        var manager = new WallpaperManager();
        int count = manager.LoadFolder(_tempDir);

        Assert.Equal(3, count);
        Assert.Equal(3, manager.TotalCount);
        Assert.Equal(0, manager.CurrentIndex);
        Assert.EndsWith("img1.png", manager.CurrentImagePath);
    }

    [Fact]
    public void NextAndPrevious_CycleCorrectly()
    {
        CreateDummyImage("a.png");
        CreateDummyImage("b.png");
        CreateDummyImage("c.png");

        var manager = new WallpaperManager();
        manager.LoadFolder(_tempDir);

        Assert.Equal(0, manager.CurrentIndex);
        Assert.EndsWith("a.png", manager.CurrentImagePath);

        // Next -> b
        manager.NextWallpaper();
        Assert.Equal(1, manager.CurrentIndex);
        Assert.EndsWith("b.png", manager.CurrentImagePath);

        // Next -> c
        manager.NextWallpaper();
        Assert.Equal(2, manager.CurrentIndex);
        Assert.EndsWith("c.png", manager.CurrentImagePath);

        // Next -> wrap around to a
        manager.NextWallpaper();
        Assert.Equal(0, manager.CurrentIndex);
        Assert.EndsWith("a.png", manager.CurrentImagePath);

        // Previous -> wrap around to c
        manager.PreviousWallpaper();
        Assert.Equal(2, manager.CurrentIndex);
        Assert.EndsWith("c.png", manager.CurrentImagePath);
    }

    [Fact]
    public void SetWallpaperByIndex_HandlesBounds()
    {
        CreateDummyImage("a.png");
        CreateDummyImage("b.png");

        var manager = new WallpaperManager();
        manager.LoadFolder(_tempDir);

        Assert.True(manager.SetWallpaperByIndex(1));
        Assert.Equal(1, manager.CurrentIndex);

        // Out of bounds
        Assert.False(manager.SetWallpaperByIndex(-1));
        Assert.False(manager.SetWallpaperByIndex(5));
        Assert.Equal(1, manager.CurrentIndex);
    }

    [Fact]
    public void WallpaperNative_ThrowsOnMissingFile()
    {
        Assert.Throws<FileNotFoundException>(() =>
            WallpaperNative.SetWallpaper(Path.Combine(_tempDir, "nonexistent.jpg")));
    }
}

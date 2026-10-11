using System;
using System.IO;
using WallChange.Core;
using Xunit;

namespace WallChange.Tests;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _settingsFilePath;

    public SettingsManagerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WallChange_SettingsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _settingsFilePath = Path.Combine(_tempDirectory, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch { }
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaultSettings()
    {
        var manager = new SettingsManager(_settingsFilePath);
        var settings = manager.Load();

        Assert.NotNull(settings);
        Assert.Null(settings.WallpaperDirectory);
        Assert.Equal("TopMiddle", settings.DockEdge);
        Assert.False(settings.IsPinned);
        Assert.Equal(WallpaperStyle.Fill, settings.WallpaperStyle);
    }

    [Fact]
    public void SaveAndLoad_PersistsValuesCorrectly()
    {
        var manager = new SettingsManager(_settingsFilePath);
        manager.Settings.WallpaperDirectory = @"C:\Pictures\Wallpapers";
        manager.Settings.LastWallpaperIndex = 3;
        manager.Settings.LastWallpaperPath = @"C:\Pictures\Wallpapers\img3.jpg";
        manager.Settings.DockEdge = "BottomMiddle";
        manager.Settings.IsPinned = true;
        manager.Settings.WallpaperStyle = WallpaperStyle.Fit;

        bool saved = manager.Save();
        Assert.True(saved);
        Assert.True(File.Exists(_settingsFilePath));

        var readerManager = new SettingsManager(_settingsFilePath);
        var loaded = readerManager.Load();

        Assert.Equal(@"C:\Pictures\Wallpapers", loaded.WallpaperDirectory);
        Assert.Equal(3, loaded.LastWallpaperIndex);
        Assert.Equal(@"C:\Pictures\Wallpapers\img3.jpg", loaded.LastWallpaperPath);
        Assert.Equal("BottomMiddle", loaded.DockEdge);
        Assert.True(loaded.IsPinned);
        Assert.Equal(WallpaperStyle.Fit, loaded.WallpaperStyle);
    }

    [Fact]
    public void UpdateWallpaperDirectory_UpdatesAndPersists()
    {
        var manager = new SettingsManager(_settingsFilePath);
        manager.UpdateWallpaperDirectory(@"D:\Wallpapers", 5, @"D:\Wallpapers\lake.png");

        Assert.True(File.Exists(_settingsFilePath));

        var freshManager = new SettingsManager(_settingsFilePath);
        freshManager.Load();

        Assert.Equal(@"D:\Wallpapers", freshManager.Settings.WallpaperDirectory);
        Assert.Equal(5, freshManager.Settings.LastWallpaperIndex);
        Assert.Equal(@"D:\Wallpapers\lake.png", freshManager.Settings.LastWallpaperPath);
    }

    [Fact]
    public void UpdateDockEdge_UpdatesAndPersists()
    {
        var manager = new SettingsManager(_settingsFilePath);
        manager.UpdateDockEdge("LeftMiddle");

        var freshManager = new SettingsManager(_settingsFilePath);
        freshManager.Load();

        Assert.Equal("LeftMiddle", freshManager.Settings.DockEdge);
    }

    [Fact]
    public void UpdatePinned_UpdatesAndPersists()
    {
        var manager = new SettingsManager(_settingsFilePath);
        manager.UpdatePinned(true);

        var freshManager = new SettingsManager(_settingsFilePath);
        freshManager.Load();

        Assert.True(freshManager.Settings.IsPinned);
    }

    [Fact]
    public void Load_WhenJsonIsCorrupted_FallsBackToDefaultsWithoutCrashing()
    {
        File.WriteAllText(_settingsFilePath, "{ invalid json content: 1234 }");

        var manager = new SettingsManager(_settingsFilePath);
        var settings = manager.Load();

        Assert.NotNull(settings);
        Assert.Null(settings.WallpaperDirectory);
        Assert.Equal("TopMiddle", settings.DockEdge);
    }

    [Fact]
    public void Load_DefaultThemeMode_IsLight()
    {
        var manager = new SettingsManager(_settingsFilePath);
        var settings = manager.Load();

        Assert.Equal(AppThemeMode.Light, settings.ThemeMode);
    }

    [Fact]
    public void UpdateThemeMode_PersistsDarkAndAdaptiveModes()
    {
        var manager = new SettingsManager(_settingsFilePath);
        manager.UpdateThemeMode(AppThemeMode.Dark);

        var reader = new SettingsManager(_settingsFilePath);
        reader.Load();
        Assert.Equal(AppThemeMode.Dark, reader.Settings.ThemeMode);

        reader.UpdateThemeMode(AppThemeMode.Adaptive);
        var reader2 = new SettingsManager(_settingsFilePath);
        reader2.Load();
        Assert.Equal(AppThemeMode.Adaptive, reader2.Settings.ThemeMode);
    }
}

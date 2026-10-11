using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace WallChange.Core;

public class SettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsFilePath;
    private AppSettings _settings;

    public string SettingsFilePath => _settingsFilePath;
    public AppSettings Settings => _settings;

    public SettingsManager(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _settingsFilePath = Path.GetFullPath(customFilePath);
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "WallChange");
            _settingsFilePath = Path.Combine(folder, "settings.json");
        }

        _settings = new AppSettings();
    }

    /// <summary>
    /// Loads settings from disk if the settings file exists.
    /// Falls back to default settings on error or if the file does not exist.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    _settings = loaded;
                    return _settings;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading settings from {_settingsFilePath}: {ex.Message}");
        }

        _settings = new AppSettings();
        return _settings;
    }

    /// <summary>
    /// Persists the current settings to disk.
    /// </summary>
    public bool Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving settings to {_settingsFilePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Updates the configured wallpaper directory and saves settings.
    /// </summary>
    public void UpdateWallpaperDirectory(string directory, int index = 0, string? imagePath = null)
    {
        _settings.WallpaperDirectory = directory;
        _settings.LastWallpaperIndex = index;
        _settings.LastWallpaperPath = imagePath;
        Save();
    }

    /// <summary>
    /// Updates the saved dock edge and saves settings.
    /// </summary>
    public void UpdateDockEdge(string dockEdge)
    {
        _settings.DockEdge = dockEdge;
        Save();
    }

    /// <summary>
    /// Updates the saved pinned state and saves settings.
    /// </summary>
    public void UpdatePinned(bool isPinned)
    {
        _settings.IsPinned = isPinned;
        Save();
    }

    /// <summary>
    /// Updates the saved wallpaper scaling style and saves settings.
    /// </summary>
    public void UpdateWallpaperStyle(WallpaperStyle style)
    {
        _settings.WallpaperStyle = style;
        Save();
    }
}

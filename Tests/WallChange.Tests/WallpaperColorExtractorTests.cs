using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WallChange.Core;
using Xunit;

namespace WallChange.Tests;

public class WallpaperColorExtractorTests : IDisposable
{
    private readonly string _tempDirectory;

    public WallpaperColorExtractorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "WallChange_ColorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
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
    public void ExtractPalette_WhenFileNullOrMissing_ReturnsDefaultPalette()
    {
        var paletteNull = WallpaperColorExtractor.ExtractPalette(null);
        var paletteMissing = WallpaperColorExtractor.ExtractPalette(Path.Combine(_tempDirectory, "does_not_exist.png"));

        Assert.NotNull(paletteNull);
        Assert.NotNull(paletteMissing);
        Assert.True(paletteNull.BackdropTop.A > 0);
        Assert.True(paletteMissing.BackdropTop.A > 0);
    }

    [Fact]
    public void BuildPalette_DarkLuminance_CreatesDarkThemeWithHighContrastText()
    {
        var dominant = Color.FromRgb(20, 30, 45);
        var accent = Color.FromRgb(50, 100, 180);
        double luminance = 0.2; // dark

        var palette = WallpaperColorExtractor.BuildPalette(dominant, accent, luminance);

        Assert.True(palette.IsDark);
        // Text primary should be near white for readability
        Assert.Equal(248, palette.TextPrimary.R);
        Assert.Equal(250, palette.TextPrimary.G);
        Assert.Equal(252, palette.TextPrimary.B);
        // Shadow should be black with noticeable opacity
        Assert.Equal(Color.FromRgb(0, 0, 0), palette.ShadowColor);
        Assert.True(palette.ShadowOpacity >= 0.4);
    }

    [Fact]
    public void BuildPalette_LightLuminance_CreatesLightThemeWithHighContrastText()
    {
        var dominant = Color.FromRgb(220, 230, 240);
        var accent = Color.FromRgb(100, 150, 220);
        double luminance = 0.85; // light

        var palette = WallpaperColorExtractor.BuildPalette(dominant, accent, luminance);

        Assert.False(palette.IsDark);
        // Text primary should be deep slate for readability
        Assert.Equal(15, palette.TextPrimary.R);
        Assert.Equal(23, palette.TextPrimary.G);
        Assert.Equal(42, palette.TextPrimary.B);
        Assert.True(palette.ShadowOpacity <= 0.25);
    }

    [Fact]
    public void ExtractPalette_WithGeneratedImage_ExtractsColorsSuccessfully()
    {
        string imagePath = Path.Combine(_tempDirectory, "test_blue.png");

        // Create a 16x16 deep blue image
        int width = 16;
        int height = 16;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 200;     // B
            pixels[i + 1] = 40;  // G
            pixels[i + 2] = 20;  // R
            pixels[i + 3] = 255; // A
        }

        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        using (var stream = new FileStream(imagePath, FileMode.Create))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(stream);
        }

        var palette = WallpaperColorExtractor.ExtractPalette(imagePath);

        Assert.NotNull(palette);
        Assert.True(palette.DominantColor.B > palette.DominantColor.R);
        Assert.True(palette.DominantColor.B > palette.DominantColor.G);
    }
}

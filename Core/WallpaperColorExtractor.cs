using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WallChange.Core;

public class ExtractedColorPalette
{
    public Color DominantColor { get; init; }
    public Color AccentColor { get; init; }
    public double Luminance { get; init; }
    public bool IsDark => Luminance < 0.5;

    public Color BackdropTop { get; init; }
    public Color BackdropBottom { get; init; }
    public Color BorderTop { get; init; }
    public Color BorderBottom { get; init; }
    public Color InnerSheenTop { get; init; }
    public Color InnerSheenBottom { get; init; }

    public Color TextPrimary { get; init; }
    public Color TextSecondary { get; init; }
    public Color TextTertiary { get; init; }
    public Color Divider { get; init; }

    public Color ButtonBg { get; init; }
    public Color ButtonHoverBg { get; init; }
    public Color ButtonPressedBg { get; init; }
    public Color ButtonBorder { get; init; }
    public Color ButtonHoverBorder { get; init; }

    public Color ShadowColor { get; init; }
    public double ShadowOpacity { get; init; }
}

public static class WallpaperColorExtractor
{
    public static ExtractedColorPalette ExtractPalette(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return GetDefaultAdaptivePalette();
        }

        try
        {
            var uri = new Uri(Path.GetFullPath(imagePath));
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.DecodePixelWidth = 80;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();

            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            converted.Freeze();

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            if (width <= 0 || height <= 0) return GetDefaultAdaptivePalette();

            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            long totalR = 0, totalG = 0, totalB = 0;
            int count = 0;

            double maxSat = -1.0;
            byte accentR = 120, accentG = 130, accentB = 145;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte b = pixels[i];
                byte g = pixels[i + 1];
                byte r = pixels[i + 2];
                byte a = pixels[i + 3];

                if (a < 50) continue;

                totalR += r;
                totalG += g;
                totalB += b;
                count++;

                byte max = Math.Max(r, Math.Max(g, b));
                byte min = Math.Min(r, Math.Min(g, b));
                double sat = max == 0 ? 0 : (double)(max - min) / max;

                if (sat > maxSat && max > 40 && min < 225)
                {
                    maxSat = sat;
                    accentR = r;
                    accentG = g;
                    accentB = b;
                }
            }

            if (count == 0) return GetDefaultAdaptivePalette();

            byte avgR = (byte)(totalR / count);
            byte avgG = (byte)(totalG / count);
            byte avgB = (byte)(totalB / count);

            double luminance = (0.299 * avgR + 0.587 * avgG + 0.114 * avgB) / 255.0;

            return BuildPalette(Color.FromRgb(avgR, avgG, avgB), Color.FromRgb(accentR, accentG, accentB), luminance);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WallpaperColorExtractor error: {ex.Message}");
            return GetDefaultAdaptivePalette();
        }
    }

    public static ExtractedColorPalette BuildPalette(Color dominant, Color accent, double luminance)
    {
        bool isDark = luminance < 0.5;

        if (isDark)
        {
            // Dark Wallpaper -> Sleek frosted dark slate tinted with wallpaper colors
            byte topR = (byte)Blend(26, dominant.R, 0.22);
            byte topG = (byte)Blend(28, dominant.G, 0.22);
            byte topB = (byte)Blend(34, dominant.B, 0.22);

            byte botR = (byte)Blend(16, dominant.R, 0.20);
            byte botG = (byte)Blend(18, dominant.G, 0.20);
            byte botB = (byte)Blend(24, dominant.B, 0.20);

            byte borderR = (byte)Blend(65, accent.R, 0.35);
            byte borderG = (byte)Blend(75, accent.G, 0.35);
            byte borderB = (byte)Blend(90, accent.B, 0.35);

            return new ExtractedColorPalette
            {
                DominantColor = dominant,
                AccentColor = accent,
                Luminance = luminance,

                BackdropTop = Color.FromArgb(244, topR, topG, topB),
                BackdropBottom = Color.FromArgb(236, botR, botG, botB),
                BorderTop = Color.FromArgb(220, borderR, borderG, borderB),
                BorderBottom = Color.FromArgb(180, (byte)Blend(38, dominant.R, 0.3), (byte)Blend(44, dominant.G, 0.3), (byte)Blend(55, dominant.B, 0.3)),
                InnerSheenTop = Color.FromArgb(30, 255, 255, 255),
                InnerSheenBottom = Color.FromArgb(0, 255, 255, 255),

                TextPrimary = Color.FromRgb(248, 250, 252),
                TextSecondary = Color.FromRgb(156, 163, 175),
                TextTertiary = Color.FromRgb(107, 114, 128),
                Divider = Color.FromArgb(120, 75, 85, 99),

                ButtonBg = Color.FromArgb(220, (byte)Blend(38, dominant.R, 0.25), (byte)Blend(42, dominant.G, 0.25), (byte)Blend(52, dominant.B, 0.25)),
                ButtonHoverBg = Color.FromArgb(245, (byte)Blend(55, dominant.R, 0.30), (byte)Blend(62, dominant.G, 0.30), (byte)Blend(75, dominant.B, 0.30)),
                ButtonPressedBg = Color.FromArgb(250, 24, 27, 34),
                ButtonBorder = Color.FromArgb(170, (byte)Blend(75, dominant.R, 0.35), (byte)Blend(85, dominant.G, 0.35), (byte)Blend(100, dominant.B, 0.35)),
                ButtonHoverBorder = Color.FromArgb(230, (byte)Blend(120, accent.R, 0.45), (byte)Blend(135, accent.G, 0.45), (byte)Blend(155, accent.B, 0.45)),

                ShadowColor = Color.FromRgb(0, 0, 0),
                ShadowOpacity = 0.45
            };
        }
        else
        {
            // Light Wallpaper -> Crisp frosted light slate tinted with wallpaper colors
            byte topR = (byte)Blend(248, dominant.R, 0.16);
            byte topG = (byte)Blend(250, dominant.G, 0.16);
            byte topB = (byte)Blend(252, dominant.B, 0.16);

            byte botR = (byte)Blend(235, dominant.R, 0.20);
            byte botG = (byte)Blend(240, dominant.G, 0.20);
            byte botB = (byte)Blend(246, dominant.B, 0.20);

            byte borderR = (byte)Blend(195, accent.R, 0.30);
            byte borderG = (byte)Blend(205, accent.G, 0.30);
            byte borderB = (byte)Blend(218, accent.B, 0.30);

            return new ExtractedColorPalette
            {
                DominantColor = dominant,
                AccentColor = accent,
                Luminance = luminance,

                BackdropTop = Color.FromArgb(246, topR, topG, topB),
                BackdropBottom = Color.FromArgb(236, botR, botG, botB),
                BorderTop = Color.FromArgb(210, borderR, borderG, borderB),
                BorderBottom = Color.FromArgb(180, (byte)Blend(145, dominant.R, 0.3), (byte)Blend(160, dominant.G, 0.3), (byte)Blend(175, dominant.B, 0.3)),
                InnerSheenTop = Color.FromArgb(144, 255, 255, 255),
                InnerSheenBottom = Color.FromArgb(0, 255, 255, 255),

                TextPrimary = Color.FromRgb(15, 23, 42),
                TextSecondary = Color.FromRgb(71, 85, 105),
                TextTertiary = Color.FromRgb(148, 163, 184),
                Divider = Color.FromArgb(160, 203, 213, 225),

                ButtonBg = Color.FromArgb(240, 255, 255, 255),
                ButtonHoverBg = Color.FromArgb(250, (byte)Blend(242, dominant.R, 0.15), (byte)Blend(245, dominant.G, 0.15), (byte)Blend(250, dominant.B, 0.15)),
                ButtonPressedBg = Color.FromArgb(250, 226, 232, 240),
                ButtonBorder = Color.FromArgb(180, 209, 213, 219),
                ButtonHoverBorder = Color.FromArgb(230, (byte)Blend(140, accent.R, 0.40), (byte)Blend(155, accent.G, 0.40), (byte)Blend(175, accent.B, 0.40)),

                ShadowColor = Color.FromRgb(15, 23, 42),
                ShadowOpacity = 0.15
            };
        }
    }

    private static int Blend(int baseVal, int tintVal, double ratio)
    {
        int res = (int)Math.Round(baseVal * (1.0 - ratio) + tintVal * ratio);
        return Math.Clamp(res, 0, 255);
    }

    public static ExtractedColorPalette GetDefaultAdaptivePalette()
    {
        return BuildPalette(Color.FromRgb(128, 140, 160), Color.FromRgb(90, 120, 170), 0.55);
    }
}

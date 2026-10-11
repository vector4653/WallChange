using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using WallChange.Core;

namespace WallChange.UI;

public partial class MainOverlayWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out DesktopDetector.POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(DesktopDetector.POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONULL = 0;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    private readonly WallpaperManager _wallpaperManager;
    private readonly DesktopDetector _desktopDetector;
    private readonly SettingsManager _settingsManager;
    private readonly DispatcherTimer _hideDelayTimer;
    private readonly DispatcherTimer _edgeHoverMonitor;

    private ScreenEdge _currentEdge = ScreenEdge.TopMiddle;
    private bool _isOpen = false;
    private bool _isUpdatingUi = false;
    private const double AnimationDurationMs = 230;
    private double _lastKnownWidth = 640;
    private double _lastKnownHeight = 60;
    private DateTime _lastSettingsPopupClosedTime = DateTime.MinValue;

    public WallpaperManager WallpaperManager => _wallpaperManager;
    public DesktopDetector DesktopDetector => _desktopDetector;
    public SettingsManager SettingsManager => _settingsManager;
    public ScreenEdge CurrentEdge => _currentEdge;
    public bool IsPanelOpen => _isOpen;

    private bool IsPinned => (TglPinH?.IsChecked == true) || (TglPinV?.IsChecked == true);

    public MainOverlayWindow()
    {
        InitializeComponent();

        _settingsManager = new SettingsManager();
        _settingsManager.Load();

        _wallpaperManager = new WallpaperManager();
        _wallpaperManager.WallpaperChanged += OnWallpaperChanged;
        _wallpaperManager.FolderLoaded += OnFolderLoaded;

        _desktopDetector = new DesktopDetector();
        _desktopDetector.DesktopActiveChanged += OnDesktopActiveChanged;

        Closed += (s, e) =>
        {
            _edgeHoverMonitor?.Stop();
            _hideDelayTimer?.Stop();
            _desktopDetector?.Dispose();
        };

        _hideDelayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(360)
        };
        _hideDelayTimer.Tick += (s, e) =>
        {
            _hideDelayTimer.Stop();
            if (!IsPinned && !IsMouseOver && SettingsPopup?.IsOpen != true)
            {
                SlideOut();
            }
        };

        // Edge cursor detector: polls screen boundary when panel is hidden
        _edgeHoverMonitor = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(35)
        };
        _edgeHoverMonitor.Tick += EdgeHoverMonitor_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        NativeWindowHelper.SetToolWindow(this);

        IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
        _desktopDetector.Start(handle);

        if (Enum.TryParse<ScreenEdge>(_settingsManager.Settings.DockEdge, out var savedEdge))
        {
            _currentEdge = savedEdge;
        }

        if (_settingsManager.Settings.IsPinned)
        {
            _isUpdatingUi = true;
            if (TglPinH != null) TglPinH.IsChecked = true;
            if (TglPinV != null) TglPinV.IsChecked = true;
            _isUpdatingUi = false;
        }

        _wallpaperManager.CurrentStyle = _settingsManager.Settings.WallpaperStyle;

        ApplyTheme(_settingsManager.Settings.ThemeMode, _settingsManager.Settings.LastWallpaperPath);
        ApplyLayoutMode(_currentEdge);
        UpdateWindowPositionAndBounds();

        // Start hidden and invisible
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            UpdateWindowPositionAndBounds();
            SnapToHiddenState(instant: true);
            _edgeHoverMonitor.Start();
        }));

        LoadInitialWallpapers();
        UpdateSettingsFolderDisplay();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ActualWidth > 0) _lastKnownWidth = ActualWidth;
        if (ActualHeight > 0) _lastKnownHeight = ActualHeight;

        UpdateWindowPositionAndBounds();
        if (!_isOpen)
        {
            SnapToHiddenState(instant: true);
        }
    }

    private void LoadInitialWallpapers()
    {
        string? savedDir = _settingsManager.Settings.WallpaperDirectory;
        bool loaded = false;

        if (!string.IsNullOrWhiteSpace(savedDir) && Directory.Exists(savedDir))
        {
            try
            {
                int count = _wallpaperManager.LoadFolder(savedDir);
                if (count > 0)
                {
                    loaded = true;
                    int targetIndex = _settingsManager.Settings.LastWallpaperIndex;
                    if (targetIndex >= 0 && targetIndex < _wallpaperManager.TotalCount)
                    {
                        _wallpaperManager.SetWallpaperByIndex(targetIndex);
                    }
                    else
                    {
                        _wallpaperManager.ApplyCurrent();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load saved wallpaper folder {savedDir}: {ex.Message}");
            }
        }

        if (!loaded)
        {
            TryLoadDefaultWindowsWallpaper();
        }

        if (_settingsManager.Settings.ThemeMode == AppThemeMode.Adaptive)
        {
            ApplyTheme(AppThemeMode.Adaptive, _wallpaperManager.CurrentImagePath);
        }
    }

    private void TryLoadDefaultWindowsWallpaper()
    {
        string defaultPath = @"C:\Windows\Web\Wallpaper";
        if (Directory.Exists(defaultPath))
        {
            var subDirs = Directory.GetDirectories(defaultPath);
            foreach (var dir in subDirs)
            {
                try
                {
                    if (_wallpaperManager.LoadFolder(dir) > 0)
                    {
                        break;
                    }
                }
                catch { }
            }
        }
    }

    public void SetScreenEdge(ScreenEdge edge)
    {
        _currentEdge = edge;
        ApplyLayoutMode(edge);
        UpdateWindowPositionAndBounds();

        if (_isOpen)
        {
            SlideIn(instant: true);
        }
        else
        {
            SnapToHiddenState(instant: true);
        }
    }

    private void ApplyLayoutMode(ScreenEdge edge)
    {
        if (HorizontalLayout == null || VerticalLayout == null) return;
        _isUpdatingUi = true;
        try
        {
            bool isVertical = (edge == ScreenEdge.LeftMiddle || edge == ScreenEdge.RightMiddle);
            if (isVertical)
            {
                HorizontalLayout.Visibility = Visibility.Collapsed;
                VerticalLayout.Visibility = Visibility.Visible;
            }
            else
            {
                VerticalLayout.Visibility = Visibility.Collapsed;
                HorizontalLayout.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _isUpdatingUi = false;
        }
    }

    public void UpdateWindowPositionAndBounds()
    {
        IntPtr hPrimary = MonitorFromPoint(new DesktopDetector.POINT(0, 0), MONITOR_DEFAULTTOPRIMARY);
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (hPrimary != IntPtr.Zero && GetMonitorInfo(hPrimary, ref mi))
        {
            PositionOnMonitor(mi.rcMonitor);
        }
    }

    private void PositionOnMonitor(RECT monRect)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double monLeftDip = monRect.Left / dpiX;
        double monTopDip = monRect.Top / dpiY;
        double monWidthDip = (monRect.Right - monRect.Left) / dpiX;
        double monHeightDip = (monRect.Bottom - monRect.Top) / dpiY;

        double width = ActualWidth > 0 ? ActualWidth : _lastKnownWidth;
        double height = ActualHeight > 0 ? ActualHeight : _lastKnownHeight;
        if (width <= 0) width = 640;
        if (height <= 0) height = 60;

        double newLeft = Left;
        double newTop = Top;

        switch (_currentEdge)
        {
            case ScreenEdge.TopMiddle:
                newLeft = Math.Round(monLeftDip + (monWidthDip - width) / 2.0);
                newTop = monTopDip;
                break;
            case ScreenEdge.BottomMiddle:
                newLeft = Math.Round(monLeftDip + (monWidthDip - width) / 2.0);
                newTop = Math.Round(monTopDip + monHeightDip - height);
                break;
            case ScreenEdge.LeftMiddle:
                newLeft = monLeftDip;
                newTop = Math.Round(monTopDip + (monHeightDip - height) / 2.0);
                break;
            case ScreenEdge.RightMiddle:
                newLeft = Math.Round(monLeftDip + monWidthDip - width);
                newTop = Math.Round(monTopDip + (monHeightDip - height) / 2.0);
                break;
        }

        if (Math.Abs(Left - newLeft) > 1 || Math.Abs(Top - newTop) > 1)
        {
            Left = newLeft;
            Top = newTop;
            if (!_isOpen)
            {
                SnapToHiddenState(instant: true);
            }
        }
    }

    private void EdgeHoverMonitor_Tick(object? sender, EventArgs e)
    {
        if (_isOpen) return;

        if (!GetCursorPos(out DesktopDetector.POINT pt)) return;

        IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONULL);
        if (hMonitor == IntPtr.Zero) return;

        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref mi)) return;

        int monWidth = mi.rcMonitor.Right - mi.rcMonitor.Left;
        int monHeight = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double widthDip = ActualWidth > 0 ? ActualWidth : _lastKnownWidth;
        double heightDip = ActualHeight > 0 ? ActualHeight : _lastKnownHeight;
        if (widthDip <= 0) widthDip = 640;
        if (heightDip <= 0) heightDip = 60;

        double widthPx = widthDip * dpiX;
        double heightPx = heightDip * dpiY;

        bool hitEdge = false;

        switch (_currentEdge)
        {
            case ScreenEdge.TopMiddle:
            {
                double dockLeftPx = mi.rcMonitor.Left + (monWidth - widthPx) / 2.0;
                double dockRightPx = dockLeftPx + widthPx;
                hitEdge = (pt.Y <= mi.rcMonitor.Top + 12) && (pt.X >= dockLeftPx - 50) && (pt.X <= dockRightPx + 50);
                break;
            }
            case ScreenEdge.BottomMiddle:
            {
                double dockLeftPx = mi.rcMonitor.Left + (monWidth - widthPx) / 2.0;
                double dockRightPx = dockLeftPx + widthPx;
                hitEdge = (pt.Y >= mi.rcMonitor.Bottom - 12) && (pt.X >= dockLeftPx - 50) && (pt.X <= dockRightPx + 50);
                break;
            }
            case ScreenEdge.LeftMiddle:
            {
                double dockTopPx = mi.rcMonitor.Top + (monHeight - heightPx) / 2.0;
                double dockBottomPx = dockTopPx + heightPx;
                hitEdge = (pt.X <= mi.rcMonitor.Left + 12) && (pt.Y >= dockTopPx - 50) && (pt.Y <= dockBottomPx + 50);
                break;
            }
            case ScreenEdge.RightMiddle:
            {
                double dockTopPx = mi.rcMonitor.Top + (monHeight - heightPx) / 2.0;
                double dockBottomPx = dockTopPx + heightPx;
                hitEdge = (pt.X >= mi.rcMonitor.Right - 12) && (pt.Y >= dockTopPx - 50) && (pt.Y <= dockBottomPx + 50);
                break;
            }
        }

        if (hitEdge)
        {
            int inwardX = _currentEdge switch
            {
                ScreenEdge.LeftMiddle => 8,
                ScreenEdge.RightMiddle => -8,
                _ => 0
            };
            int inwardY = _currentEdge switch
            {
                ScreenEdge.TopMiddle => 8,
                ScreenEdge.BottomMiddle => -8,
                _ => 0
            };

            if (IsPinned || _desktopDetector.IsDesktopActiveForPoint(pt, inwardX, inwardY))
            {
                PositionOnMonitor(mi.rcMonitor);
                SlideIn();
            }
        }
    }

    public void SlideIn(bool instant = false)
    {
        _hideDelayTimer.Stop();
        _isOpen = true;
        Visibility = Visibility.Visible;
        Opacity = 1.0;

        var targetProperty = (_currentEdge == ScreenEdge.LeftMiddle || _currentEdge == ScreenEdge.RightMiddle)
            ? TranslateTransform.XProperty
            : TranslateTransform.YProperty;

        if (instant)
        {
            PanelTransform.BeginAnimation(targetProperty, null);
            if (targetProperty == TranslateTransform.XProperty) PanelTransform.X = 0;
            else PanelTransform.Y = 0;
            return;
        }

        var anim = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        PanelTransform.BeginAnimation(targetProperty, anim);
    }

    public void SlideOut(bool instant = false)
    {
        if (IsPinned || SettingsPopup?.IsOpen == true) return;

        _isOpen = false;
        double targetVal = GetHiddenOffset();

        var targetProperty = (_currentEdge == ScreenEdge.LeftMiddle || _currentEdge == ScreenEdge.RightMiddle)
            ? TranslateTransform.XProperty
            : TranslateTransform.YProperty;

        if (instant)
        {
            PanelTransform.BeginAnimation(targetProperty, null);
            if (targetProperty == TranslateTransform.XProperty) PanelTransform.X = targetVal;
            else PanelTransform.Y = targetVal;
            Opacity = 0.0;
            Visibility = Visibility.Hidden;
            return;
        }

        var anim = new DoubleAnimation
        {
            To = targetVal,
            Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        anim.Completed += (s, e) =>
        {
            if (!_isOpen)
            {
                Opacity = 0.0;
                Visibility = Visibility.Hidden;
            }
        };

        PanelTransform.BeginAnimation(targetProperty, anim);
    }

    private void SnapToHiddenState(bool instant = true)
    {
        PanelTransform.BeginAnimation(TranslateTransform.XProperty, null);
        PanelTransform.BeginAnimation(TranslateTransform.YProperty, null);

        double hiddenOffset = GetHiddenOffset();
        if (_currentEdge == ScreenEdge.LeftMiddle || _currentEdge == ScreenEdge.RightMiddle)
        {
            PanelTransform.X = hiddenOffset;
            PanelTransform.Y = 0;
        }
        else
        {
            PanelTransform.X = 0;
            PanelTransform.Y = hiddenOffset;
        }
        _isOpen = false;
        Opacity = 0.0;
        Visibility = Visibility.Hidden;
    }

    private double GetHiddenOffset()
    {
        double width = ActualWidth > 0 ? ActualWidth : _lastKnownWidth;
        double height = ActualHeight > 0 ? ActualHeight : _lastKnownHeight;
        const double ExtraMargin = 40.0;

        return _currentEdge switch
        {
            ScreenEdge.TopMiddle => -(height + ExtraMargin),
            ScreenEdge.BottomMiddle => (height + ExtraMargin),
            ScreenEdge.LeftMiddle => -(width + ExtraMargin),
            ScreenEdge.RightMiddle => (width + ExtraMargin),
            _ => -(height + ExtraMargin)
        };
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        if (IsPinned || _desktopDetector.CheckCurrentState())
        {
            SlideIn();
        }
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isOpen && (IsPinned || _desktopDetector.CheckCurrentState()))
        {
            SlideIn();
        }
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!IsPinned && SettingsPopup?.IsOpen != true)
        {
            _hideDelayTimer.Stop();
            _hideDelayTimer.Start();
        }
    }

    private void OnDesktopActiveChanged(object? sender, bool isDesktopActive)
    {
        Dispatcher.Invoke(() =>
        {
            if (!isDesktopActive && _isOpen && !IsPinned && SettingsPopup?.IsOpen != true)
            {
                SlideOut();
            }
        });
    }

    private void TglPin_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUi) return;

        bool isChecked = (sender as ToggleButton)?.IsChecked == true;
        _isUpdatingUi = true;
        if (TglPinH != null) TglPinH.IsChecked = isChecked;
        if (TglPinV != null) TglPinV.IsChecked = isChecked;
        _isUpdatingUi = false;

        _settingsManager.UpdatePinned(isChecked);

        if (isChecked)
        {
            SlideIn();
        }
    }

    private void BtnPrevious_Click(object sender, RoutedEventArgs e)
    {
        _wallpaperManager.PreviousWallpaper();
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        _wallpaperManager.NextWallpaper();
    }

    private void BtnRandom_Click(object sender, RoutedEventArgs e)
    {
        _wallpaperManager.RandomWallpaper();
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Wallpaper Folder",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                int count = _wallpaperManager.LoadFolder(dialog.FolderName);
                if (count > 0)
                {
                    _wallpaperManager.ApplyCurrent();
                    _settingsManager.UpdateWallpaperDirectory(dialog.FolderName, _wallpaperManager.CurrentIndex, _wallpaperManager.CurrentImagePath);
                    UpdateSettingsFolderDisplay();
                }
                else
                {
                    MessageBox.Show(this, "No supported image files (.jpg, .png, .bmp, .webp) found in the selected folder.",
                        "Empty Folder", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnFolderLoaded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStatusDisplay();
            UpdateSettingsFolderDisplay();
            if (_settingsManager.Settings.ThemeMode == AppThemeMode.Adaptive)
            {
                ApplyTheme(AppThemeMode.Adaptive, _wallpaperManager.CurrentImagePath);
            }
        });
    }

    private void OnWallpaperChanged(object? sender, WallpaperChangedEventArgs e)
    {
        _settingsManager.Settings.LastWallpaperIndex = e.Index;
        _settingsManager.Settings.LastWallpaperPath = e.ImagePath;
        _settingsManager.Save();
        Dispatcher.Invoke(() =>
        {
            UpdateStatusDisplay();
            if (_settingsManager.Settings.ThemeMode == AppThemeMode.Adaptive)
            {
                ApplyTheme(AppThemeMode.Adaptive, e.ImagePath);
            }
        });
    }

    private void UpdateStatusDisplay()
    {
        string nameText = "No Images Found";
        string countText = "Click Settings to pick folder";

        if (_wallpaperManager.TotalCount > 0 && !string.IsNullOrEmpty(_wallpaperManager.CurrentImagePath))
        {
            nameText = Path.GetFileName(_wallpaperManager.CurrentImagePath);
            countText = $"{_wallpaperManager.CurrentIndex + 1} of {_wallpaperManager.TotalCount} images";
        }

        if (TxtWallpaperNameH != null) TxtWallpaperNameH.Text = nameText;
        if (TxtWallpaperIndexH != null) TxtWallpaperIndexH.Text = countText;

        if (TxtWallpaperNameV != null) TxtWallpaperNameV.Text = nameText;
        if (TxtWallpaperIndexV != null) TxtWallpaperIndexV.Text = countText;
    }

    private void UpdateSettingsFolderDisplay()
    {
        if (BtnSettingsBrowseFolder == null) return;
        string? current = _wallpaperManager.CurrentFolderPath ?? _settingsManager.Settings.WallpaperDirectory;
        if (!string.IsNullOrWhiteSpace(current))
        {
            BtnSettingsBrowseFolder.ToolTip = $"Folder: {current}";
        }
        else
        {
            BtnSettingsBrowseFolder.ToolTip = @"Default Windows Wallpapers (C:\Windows\Web\Wallpaper)";
        }
    }

    // =========================================================================
    // SETTINGS FLYOUT & THEME MANAGEMENT
    // =========================================================================

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsPopup == null) return;

        if (SettingsPopup.IsOpen)
        {
            SettingsPopup.IsOpen = false;
            return;
        }

        // If the popup was just closed within 250ms (e.g. by clicking on this button while open),
        // don't immediately reopen it.
        if ((DateTime.UtcNow - _lastSettingsPopupClosedTime).TotalMilliseconds < 250)
        {
            return;
        }

        var btn = sender as FrameworkElement ?? (HorizontalLayout.Visibility == Visibility.Visible ? (FrameworkElement)BtnSettingsH : (FrameworkElement)BtnSettingsV);

        // Target CardBorder for rock-solid popup anchoring, preventing WPF button-capture auto-close issues
        SettingsPopup.PlacementTarget = CardBorder;

        const double popupTotalWidth = 310.0;
        const double popupTotalHeight = 220.0;

        double btnCenterX = CardBorder.ActualWidth - 75.0;
        double btnCenterY = CardBorder.ActualHeight / 2.0;

        if (btn != null)
        {
            try
            {
                Point p = btn.TranslatePoint(new Point(0, 0), CardBorder);
                btnCenterX = p.X + (btn.ActualWidth > 0 ? btn.ActualWidth / 2.0 : 35.0);
                btnCenterY = p.Y + (btn.ActualHeight > 0 ? btn.ActualHeight / 2.0 : 16.0);
            }
            catch
            {
                // Fallback to approximate button center if TranslatePoint fails
            }
        }

        switch (_currentEdge)
        {
            case ScreenEdge.TopMiddle:
            {
                SettingsPopup.Placement = PlacementMode.Bottom;
                double hOffset = btnCenterX - (popupTotalWidth / 2.0);
                double maxHOffset = Math.Max(0, CardBorder.ActualWidth - popupTotalWidth + 10);
                if (hOffset > maxHOffset) hOffset = maxHOffset;
                if (hOffset < 0) hOffset = 0;
                SettingsPopup.HorizontalOffset = hOffset;
                SettingsPopup.VerticalOffset = -3;
                break;
            }

            case ScreenEdge.BottomMiddle:
            {
                SettingsPopup.Placement = PlacementMode.Top;
                double hOffset = btnCenterX - (popupTotalWidth / 2.0);
                double maxHOffset = Math.Max(0, CardBorder.ActualWidth - popupTotalWidth + 10);
                if (hOffset > maxHOffset) hOffset = maxHOffset;
                if (hOffset < 0) hOffset = 0;
                SettingsPopup.HorizontalOffset = hOffset;
                SettingsPopup.VerticalOffset = 3;
                break;
            }

            case ScreenEdge.LeftMiddle:
            {
                SettingsPopup.Placement = PlacementMode.Right;
                double vOffset = btnCenterY - (popupTotalHeight / 2.0);
                double maxVOffset = Math.Max(0, CardBorder.ActualHeight - popupTotalHeight + 10);
                if (vOffset > maxVOffset) vOffset = maxVOffset;
                if (vOffset < 0) vOffset = 0;
                SettingsPopup.HorizontalOffset = 3;
                SettingsPopup.VerticalOffset = vOffset;
                break;
            }

            case ScreenEdge.RightMiddle:
            {
                SettingsPopup.Placement = PlacementMode.Left;
                double vOffset = btnCenterY - (popupTotalHeight / 2.0);
                double maxVOffset = Math.Max(0, CardBorder.ActualHeight - popupTotalHeight + 10);
                if (vOffset > maxVOffset) vOffset = maxVOffset;
                if (vOffset < 0) vOffset = 0;
                SettingsPopup.HorizontalOffset = -3;
                SettingsPopup.VerticalOffset = vOffset;
                break;
            }

            default:
            {
                SettingsPopup.Placement = PlacementMode.Bottom;
                double hOffset = btnCenterX - (popupTotalWidth / 2.0);
                double maxHOffset = Math.Max(0, CardBorder.ActualWidth - popupTotalWidth + 10);
                if (hOffset > maxHOffset) hOffset = maxHOffset;
                if (hOffset < 0) hOffset = 0;
                SettingsPopup.HorizontalOffset = hOffset;
                SettingsPopup.VerticalOffset = -3;
                break;
            }
        }

        _isUpdatingUi = true;
        try
        {
            if (CmbSettingsTheme != null)
            {
                CmbSettingsTheme.SelectedIndex = (int)_settingsManager.Settings.ThemeMode;
            }

            if (CmbSettingsPosition != null)
            {
                CmbSettingsPosition.SelectedIndex = _currentEdge switch
                {
                    ScreenEdge.TopMiddle => 0,
                    ScreenEdge.BottomMiddle => 1,
                    ScreenEdge.LeftMiddle => 2,
                    ScreenEdge.RightMiddle => 3,
                    _ => 0
                };
            }

            UpdateSettingsFolderDisplay();
        }
        finally
        {
            _isUpdatingUi = false;
        }

        SettingsPopup.IsOpen = true;
    }

    private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsPopup != null)
        {
            SettingsPopup.IsOpen = false;
        }
    }

    private void SettingsPopup_Opened(object? sender, EventArgs e)
    {
        _hideDelayTimer.Stop();
    }

    private void SettingsPopup_Closed(object? sender, EventArgs e)
    {
        _lastSettingsPopupClosedTime = DateTime.UtcNow;
        if (!IsPinned && !IsMouseOver)
        {
            _hideDelayTimer.Stop();
            _hideDelayTimer.Start();
        }
    }

    private void CmbSettingsTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _isUpdatingUi) return;

        if (CmbSettingsTheme == null || CmbSettingsTheme.SelectedIndex < 0) return;

        var selectedTheme = (AppThemeMode)CmbSettingsTheme.SelectedIndex;
        ApplyTheme(selectedTheme, _wallpaperManager.CurrentImagePath);
        _settingsManager.UpdateThemeMode(selectedTheme);
    }

    private void CmbSettingsPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _isUpdatingUi) return;

        if (CmbSettingsPosition == null || CmbSettingsPosition.SelectedIndex < 0) return;

        var edge = CmbSettingsPosition.SelectedIndex switch
        {
            0 => ScreenEdge.TopMiddle,
            1 => ScreenEdge.BottomMiddle,
            2 => ScreenEdge.LeftMiddle,
            3 => ScreenEdge.RightMiddle,
            _ => ScreenEdge.TopMiddle
        };

        if (edge != _currentEdge)
        {
            if (SettingsPopup != null && SettingsPopup.IsOpen)
            {
                SettingsPopup.IsOpen = false;
            }

            SetScreenEdge(edge);
            _settingsManager.UpdateDockEdge(edge.ToString());
        }
    }

    public void ApplyTheme(AppThemeMode themeMode, string? currentWallpaperPath = null)
    {
        switch (themeMode)
        {
            case AppThemeMode.Light:
                ApplyLightPalette();
                break;
            case AppThemeMode.Dark:
                ApplyDarkPalette();
                break;
            case AppThemeMode.Adaptive:
                ApplyAdaptivePalette(currentWallpaperPath);
                break;
        }
    }

    private void ApplyLightPalette()
    {
        UpdateGradientBrush("MicaBackdropBrush", Color.FromArgb(244, 248, 250, 252), Color.FromArgb(232, 241, 245, 249));
        UpdateGradientBrush("MicaBorderBrush", Color.FromRgb(203, 213, 225), Color.FromRgb(148, 163, 184));
        UpdateGradientBrush("MicaInnerSheenBrush", Color.FromArgb(144, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));

        UpdateSolidBrush("BtnBgBrush", Color.FromRgb(255, 255, 255));
        UpdateSolidBrush("BtnHoverBgBrush", Color.FromRgb(241, 245, 249));
        UpdateSolidBrush("BtnPressedBgBrush", Color.FromRgb(226, 232, 240));
        UpdateSolidBrush("BtnBorderBrush", Color.FromRgb(209, 213, 219));
        UpdateSolidBrush("BtnHoverBorderBrush", Color.FromRgb(148, 163, 184));

        UpdateSolidBrush("TextPrimaryBrush", Color.FromRgb(15, 23, 42));
        UpdateSolidBrush("TextSecondaryBrush", Color.FromRgb(71, 85, 105));
        UpdateSolidBrush("TextTertiaryBrush", Color.FromRgb(148, 163, 184));
        UpdateSolidBrush("DividerBrush", Color.FromRgb(203, 213, 225));
        UpdateSolidBrush("MenuBgBrush", Color.FromRgb(255, 255, 255));

        if (CardShadow != null)
        {
            CardShadow.Color = Color.FromRgb(15, 23, 42);
            CardShadow.Opacity = 0.14;
        }
    }

    private void ApplyDarkPalette()
    {
        UpdateGradientBrush("MicaBackdropBrush", Color.FromArgb(240, 30, 34, 42), Color.FromArgb(232, 20, 23, 30));
        UpdateGradientBrush("MicaBorderBrush", Color.FromRgb(55, 65, 81), Color.FromRgb(31, 41, 55));
        UpdateGradientBrush("MicaInnerSheenBrush", Color.FromArgb(25, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));

        UpdateSolidBrush("BtnBgBrush", Color.FromRgb(38, 43, 53));
        UpdateSolidBrush("BtnHoverBgBrush", Color.FromRgb(55, 65, 81));
        UpdateSolidBrush("BtnPressedBgBrush", Color.FromRgb(26, 29, 36));
        UpdateSolidBrush("BtnBorderBrush", Color.FromRgb(75, 85, 99));
        UpdateSolidBrush("BtnHoverBorderBrush", Color.FromRgb(107, 114, 128));

        UpdateSolidBrush("TextPrimaryBrush", Color.FromRgb(249, 250, 251));
        UpdateSolidBrush("TextSecondaryBrush", Color.FromRgb(156, 163, 175));
        UpdateSolidBrush("TextTertiaryBrush", Color.FromRgb(107, 114, 128));
        UpdateSolidBrush("DividerBrush", Color.FromRgb(55, 65, 81));
        UpdateSolidBrush("MenuBgBrush", Color.FromRgb(30, 35, 45));

        if (CardShadow != null)
        {
            CardShadow.Color = Color.FromRgb(0, 0, 0);
            CardShadow.Opacity = 0.45;
        }
    }

    private void ApplyAdaptivePalette(string? imagePath)
    {
        var palette = WallpaperColorExtractor.ExtractPalette(imagePath);

        UpdateGradientBrush("MicaBackdropBrush", palette.BackdropTop, palette.BackdropBottom);
        UpdateGradientBrush("MicaBorderBrush", palette.BorderTop, palette.BorderBottom);
        UpdateGradientBrush("MicaInnerSheenBrush", palette.InnerSheenTop, palette.InnerSheenBottom);

        UpdateSolidBrush("BtnBgBrush", palette.ButtonBg);
        UpdateSolidBrush("BtnHoverBgBrush", palette.ButtonHoverBg);
        UpdateSolidBrush("BtnPressedBgBrush", palette.ButtonPressedBg);
        UpdateSolidBrush("BtnBorderBrush", palette.ButtonBorder);
        UpdateSolidBrush("BtnHoverBorderBrush", palette.ButtonHoverBorder);

        UpdateSolidBrush("TextPrimaryBrush", palette.TextPrimary);
        UpdateSolidBrush("TextSecondaryBrush", palette.TextSecondary);
        UpdateSolidBrush("TextTertiaryBrush", palette.TextTertiary);
        UpdateSolidBrush("DividerBrush", palette.Divider);

        Color menuBg = palette.IsDark
            ? Color.FromRgb(palette.BackdropBottom.R, palette.BackdropBottom.G, palette.BackdropBottom.B)
            : Color.FromRgb(255, 255, 255);
        UpdateSolidBrush("MenuBgBrush", menuBg);

        if (CardShadow != null)
        {
            CardShadow.Color = palette.ShadowColor;
            CardShadow.Opacity = palette.ShadowOpacity;
        }
    }

    private void UpdateSolidBrush(string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Resources[key] = brush;
    }

    private void UpdateGradientBrush(string key, Color stop1, Color stop2)
    {
        var grad = new LinearGradientBrush(stop1, stop2, new Point(0, 0), new Point(0, 1));
        grad.Freeze();
        Resources[key] = grad;
    }
}

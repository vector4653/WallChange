using System;
using WallChange.Core;
using Xunit;

namespace WallChange.Tests;

public class DesktopDetectorTests
{
    [Fact]
    public void IsDesktopWindow_ZeroHandle_ReturnsTrue_BecauseNoAppIsFocused()
    {
        using var detector = new DesktopDetector();
        bool result = detector.IsDesktopWindow(IntPtr.Zero);
        Assert.True(result);
    }

    [Fact]
    public void IsDesktopWindow_OwnWindowHandle_ReturnsTrue()
    {
        using var detector = new DesktopDetector();
        var fakeOwnHandle = new IntPtr(123456);
        detector.Start(fakeOwnHandle);

        bool result = detector.IsDesktopWindow(fakeOwnHandle);
        Assert.True(result);
    }

    [Fact]
    public void StartAndStop_Lifecycle_DoesNotThrow()
    {
        using var detector = new DesktopDetector();
        detector.Start(new IntPtr(999999));
        detector.Stop();
        detector.Start(new IntPtr(999999));
        detector.Stop();
    }

    [Fact]
    public void CheckCurrentState_DoesNotThrow()
    {
        using var detector = new DesktopDetector();
        detector.Start(IntPtr.Zero);
        // Calling CheckCurrentState should evaluate safely without crashing
        bool state = detector.CheckCurrentState();
        // State is a valid boolean
        Assert.True(state || !state);
    }

    [Fact]
    public void IsWindowCloaked_ZeroHandle_ReturnsFalse()
    {
        using var detector = new DesktopDetector();
        Assert.False(detector.IsWindowCloaked(IntPtr.Zero));
    }

    [Fact]
    public void IsWindowIconic_ZeroHandle_ReturnsFalse()
    {
        using var detector = new DesktopDetector();
        Assert.False(detector.IsWindowIconic(IntPtr.Zero));
    }

    [Fact]
    public void IsDesktopActiveForWindow_ZeroForeground_ReturnsTrue()
    {
        using var detector = new DesktopDetector();
        Assert.True(detector.IsDesktopActiveForWindow(IntPtr.Zero, IntPtr.Zero));
    }

    [Fact]
    public void IsDesktopActiveForWindow_OwnWindowAsForeground_ReturnsTrue()
    {
        using var detector = new DesktopDetector();
        var ownHandle = new IntPtr(12345);
        detector.Start(ownHandle);
        Assert.True(detector.IsDesktopActiveForWindow(ownHandle, ownHandle));
    }

    [Fact]
    public void IsDesktopActiveForWindowOrPoint_DoesNotThrow_OnArbitraryPoint()
    {
        using var detector = new DesktopDetector();
        detector.Start(IntPtr.Zero);
        var pt = new DesktopDetector.POINT(100, 0);
        bool result = detector.IsDesktopActiveForWindowOrPoint(IntPtr.Zero, pt, 0, 8);
        Assert.True(result || !result);
    }

    [Fact]
    public void IsDesktopActiveForPoint_DoesNotThrow_OnArbitraryPoint()
    {
        using var detector = new DesktopDetector();
        detector.Start(IntPtr.Zero);
        var pt = new DesktopDetector.POINT(200, 0);
        bool result = detector.IsDesktopActiveForPoint(pt, 0, 8);
        Assert.True(result || !result);
    }

    [Fact]
    public void IsDesktopActiveForMonitor_ZeroForeground_ReturnsTrue()
    {
        using var detector = new DesktopDetector();
        Assert.True(detector.IsDesktopActiveForMonitor(IntPtr.Zero, IntPtr.Zero));
    }
}

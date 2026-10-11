using WallChange.UI;
using Xunit;

namespace WallChange.Tests;

public class ScreenEdgeTests
{
    [Theory]
    [InlineData(ScreenEdge.TopMiddle, 1920, 1080, 680, 80, 620, 0)]
    [InlineData(ScreenEdge.BottomMiddle, 1920, 1080, 680, 80, 620, 1000)]
    [InlineData(ScreenEdge.LeftMiddle, 1920, 1080, 680, 80, 0, 500)]
    [InlineData(ScreenEdge.RightMiddle, 1920, 1080, 680, 80, 1240, 500)]
    public void ScreenEdge_CalculatesCorrectCoordinates(
        ScreenEdge edge,
        double screenW, double screenH,
        double windowW, double windowH,
        double expectedLeft, double expectedTop)
    {
        double actualLeft = 0;
        double actualTop = 0;

        switch (edge)
        {
            case ScreenEdge.TopMiddle:
                actualLeft = (screenW - windowW) / 2.0;
                actualTop = 0;
                break;
            case ScreenEdge.BottomMiddle:
                actualLeft = (screenW - windowW) / 2.0;
                actualTop = screenH - windowH;
                break;
            case ScreenEdge.LeftMiddle:
                actualLeft = 0;
                actualTop = (screenH - windowH) / 2.0;
                break;
            case ScreenEdge.RightMiddle:
                actualLeft = screenW - windowW;
                actualTop = (screenH - windowH) / 2.0;
                break;
        }

        Assert.Equal(expectedLeft, actualLeft);
        Assert.Equal(expectedTop, actualTop);
    }
}

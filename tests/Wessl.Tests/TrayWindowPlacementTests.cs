using Wessl.App.Services;

namespace Wessl.Tests;

public sealed class TrayWindowPlacementTests
{
    private static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);
    private static readonly WindowDimensions Window = new(400, 300);

    [Theory]
    [InlineData((int)TaskbarEdge.Bottom, 800, 740)]
    [InlineData((int)TaskbarEdge.Top, 800, 40)]
    [InlineData((int)TaskbarEdge.Left, 40, 350)]
    [InlineData((int)TaskbarEdge.Right, 1480, 350)]
    public void PositionsWindowAtTaskbarEdgeCenteredOnCursor(
        int edgeValue,
        int expectedX,
        int expectedY)
    {
        var edge = (TaskbarEdge)edgeValue;
        var workArea = edge switch
        {
            TaskbarEdge.Bottom => new ScreenRect(0, 0, 1920, 1040),
            TaskbarEdge.Top => new ScreenRect(0, 40, 1920, 1080),
            TaskbarEdge.Left => new ScreenRect(40, 0, 1920, 1080),
            TaskbarEdge.Right => new ScreenRect(0, 0, 1880, 1080),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
        var cursor = edge switch
        {
            TaskbarEdge.Bottom => new ScreenPoint(1000, 1060),
            TaskbarEdge.Top => new ScreenPoint(1000, 20),
            TaskbarEdge.Left => new ScreenPoint(20, 500),
            TaskbarEdge.Right => new ScreenPoint(1900, 500),
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };

        var position = TrayWindowPlacement.CalculatePosition(workArea, Window, edge, cursor);

        Assert.Equal(new ScreenPoint(expectedX, expectedY), position);
    }

    [Fact]
    public void CentersOnEdgeAndClampsWindowWithinWorkAreaNearItsCorner()
    {
        var workArea = new ScreenRect(0, 0, 1920, 1040);

        var position = TrayWindowPlacement.CalculatePosition(
            workArea,
            Window,
            TaskbarEdge.Bottom,
            new ScreenPoint(5, 1070));

        Assert.Equal(new ScreenPoint(0, 740), position);
    }

    [Fact]
    public void UsesSafeWorkAreaCenterWhenCursorIsUnavailable()
    {
        var workArea = new ScreenRect(-1920, 40, 0, 1080);

        var position = TrayWindowPlacement.CalculatePosition(
            workArea,
            Window,
            TaskbarEdge.Top,
            null);

        Assert.Equal(new ScreenPoint(-1160, 40), position);
    }

    [Fact]
    public void UsesPhysicalPixelDimensionsOnASecondaryHighDpiMonitor()
    {
        var workArea = new ScreenRect(-2560, 0, 0, 1400);

        var position = TrayWindowPlacement.CalculatePosition(
            workArea,
            new WindowDimensions(800, 600),
            TaskbarEdge.Bottom,
            new ScreenPoint(-1280, 1430));

        Assert.Equal(new ScreenPoint(-1680, 800), position);
    }

    [Theory]
    [InlineData((int)TaskbarEdge.Left, 80, 0, 1920, 1080)]
    [InlineData((int)TaskbarEdge.Top, 0, 80, 1920, 1080)]
    [InlineData((int)TaskbarEdge.Right, 0, 0, 1840, 1080)]
    [InlineData((int)TaskbarEdge.Bottom, 0, 0, 1920, 1000)]
    public void InfersVisibleTaskbarEdgeFromMonitorWorkAreaInsets(
        int expectedValue,
        int workLeft,
        int workTop,
        int workRight,
        int workBottom)
    {
        var edge = TrayWindowPlacement.InferTaskbarEdge(
            Monitor,
            new ScreenRect(workLeft, workTop, workRight, workBottom));

        Assert.Equal((TaskbarEdge)expectedValue, edge);
    }

    [Fact]
    public void UsesReportedAutoHideEdgeWhenWorkAreaHasNoInset()
    {
        var edge = TrayWindowPlacement.InferTaskbarEdge(
            Monitor,
            Monitor,
            TaskbarEdge.Left);

        Assert.Equal(TaskbarEdge.Left, edge);
    }
}

namespace Wessl.App.Services;

internal enum TaskbarEdge
{
    Left,
    Top,
    Right,
    Bottom
}

internal readonly record struct ScreenPoint(int X, int Y);

internal readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
}

internal readonly record struct WindowDimensions(int Width, int Height);

internal static class TrayWindowPlacement
{
    public static TaskbarEdge InferTaskbarEdge(
        ScreenRect monitor,
        ScreenRect workArea,
        TaskbarEdge? autoHideEdge = null)
    {
        if (autoHideEdge is { } edge)
        {
            return edge;
        }

        var insets = new[]
        {
            (Edge: TaskbarEdge.Left, Size: Math.Max(0, workArea.Left - monitor.Left)),
            (Edge: TaskbarEdge.Top, Size: Math.Max(0, workArea.Top - monitor.Top)),
            (Edge: TaskbarEdge.Right, Size: Math.Max(0, monitor.Right - workArea.Right)),
            (Edge: TaskbarEdge.Bottom, Size: Math.Max(0, monitor.Bottom - workArea.Bottom))
        };
        var largestInset = insets.Max(inset => inset.Size);
        return largestInset == 0
            ? TaskbarEdge.Bottom
            : insets.First(inset => inset.Size == largestInset).Edge;
    }

    public static ScreenPoint CalculatePosition(
        ScreenRect workArea,
        WindowDimensions window,
        TaskbarEdge edge,
        ScreenPoint? cursorPosition)
    {
        var anchor = cursorPosition ?? new ScreenPoint(
            workArea.Left + workArea.Width / 2,
            workArea.Top + workArea.Height / 2);
        var x = edge switch
        {
            TaskbarEdge.Left => workArea.Left,
            TaskbarEdge.Right => workArea.Right - window.Width,
            _ => anchor.X - window.Width / 2
        };
        var y = edge switch
        {
            TaskbarEdge.Top => workArea.Top,
            TaskbarEdge.Bottom => workArea.Bottom - window.Height,
            _ => anchor.Y - window.Height / 2
        };

        return new ScreenPoint(
            Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - window.Width)),
            Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - window.Height)));
    }
}

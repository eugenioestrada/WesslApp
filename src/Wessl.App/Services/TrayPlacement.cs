using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Wessl.App.Services;

internal static class TrayPlacement
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint MonitorDefaultToNull = 0;
    private const uint MonitorDefaultToPrimary = 1;
    private const uint AbmGetAutoHideBarEx = 0x0000000B;
    private const uint AppBarEdgeLeft = 0;
    private const uint AppBarEdgeTop = 1;
    private const uint AppBarEdgeRight = 2;
    private const uint AppBarEdgeBottom = 3;

    public static PointInt32? GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            Trace.TraceWarning("Windows could not provide the current cursor position.");
            return null;
        }

        return new PointInt32(point.X, point.Y);
    }

    public static void MoveMenuHost(AppWindow window, PointInt32? cursorPosition)
    {
        var monitor = GetMonitor(cursorPosition);
        var workArea = ToScreenRect(monitor.Info.WorkArea);
        var target = cursorPosition is { } cursor
            ? new ScreenPoint(cursor.X, cursor.Y)
            : new ScreenPoint(
                workArea.Left + workArea.Width / 2,
                workArea.Top + workArea.Height / 2);
        window.Move(new PointInt32(
            Math.Clamp(target.X, workArea.Left, Math.Max(workArea.Left, workArea.Right - 1)),
            Math.Clamp(target.Y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - 1))));
    }

    public static void MoveWindowAdjacentToTaskbar(AppWindow window, PointInt32? cursorPosition)
    {
        var (monitorHandle, monitorInfo) = GetMonitor(cursorPosition);
        var monitorArea = ToScreenRect(monitorInfo.MonitorArea);
        var workArea = ToScreenRect(monitorInfo.WorkArea);
        var edge = GetTaskbarEdge(monitorHandle, monitorInfo);
        // AppWindow sizes are physical pixels; move to the target monitor before measuring.
        window.Move(new PointInt32(workArea.Left, workArea.Top));
        var position = TrayWindowPlacement.CalculatePosition(
            workArea,
            new WindowDimensions(window.Size.Width, window.Size.Height),
            edge,
            cursorPosition is { } cursor ? new ScreenPoint(cursor.X, cursor.Y) : null);
        window.Move(new PointInt32(position.X, position.Y));
    }

    private static (IntPtr Handle, MonitorInfo Info) GetMonitor(PointInt32? cursorPosition)
    {
        var point = cursorPosition is { } cursor
            ? new NativePoint { X = cursor.X, Y = cursor.Y }
            : new NativePoint();
        var fallback = cursorPosition is null
            ? MonitorDefaultToPrimary
            : MonitorDefaultToNearest;
        var handle = MonitorFromPoint(point, fallback);
        if (handle == IntPtr.Zero && cursorPosition is not null)
        {
            handle = MonitorFromPoint(new NativePoint(), MonitorDefaultToPrimary);
        }
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("The monitor for the tray window is not available.");
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(handle, ref info))
        {
            throw new InvalidOperationException("The tray window monitor information is unavailable.");
        }

        return (handle, info);
    }

    private static TaskbarEdge GetTaskbarEdge(IntPtr monitorHandle, MonitorInfo monitorInfo)
    {
        var monitorArea = monitorInfo.MonitorArea;
        var autoHideEdge = GetAutoHideTaskbarEdge(monitorArea);
        if (autoHideEdge is { } autoHide)
        {
            return autoHide;
        }

        if (TryGetVisibleTaskbarEdge(monitorHandle, monitorArea, out var visibleEdge))
        {
            return visibleEdge;
        }

        return TrayWindowPlacement.InferTaskbarEdge(
            ToScreenRect(monitorArea),
            ToScreenRect(monitorInfo.WorkArea));
    }

    private static TaskbarEdge? GetAutoHideTaskbarEdge(NativeRect monitorArea)
    {
        foreach (var (appBarEdge, taskbarEdge) in new[]
        {
            (AppBarEdgeLeft, TaskbarEdge.Left),
            (AppBarEdgeTop, TaskbarEdge.Top),
            (AppBarEdgeRight, TaskbarEdge.Right),
            (AppBarEdgeBottom, TaskbarEdge.Bottom)
        })
        {
            var data = new AppBarData
            {
                Size = Marshal.SizeOf<AppBarData>(),
                Edge = appBarEdge,
                Rect = monitorArea
            };
            var handle = SHAppBarMessage(AbmGetAutoHideBarEx, ref data);
            if (handle != IntPtr.Zero && IsTaskbarWindow(handle))
            {
                return taskbarEdge;
            }
        }

        return null;
    }

    private static bool TryGetVisibleTaskbarEdge(
        IntPtr monitorHandle,
        NativeRect monitorArea,
        out TaskbarEdge edge)
    {
        TaskbarEdge? foundEdge = null;
        EnumWindows((handle, _) =>
        {
            if (!IsTaskbarWindow(handle)
                || MonitorFromWindow(handle, MonitorDefaultToNull) != monitorHandle
                || !GetWindowRect(handle, out var windowArea))
            {
                return true;
            }

            var width = windowArea.Right - windowArea.Left;
            var height = windowArea.Bottom - windowArea.Top;
            var monitorWidth = monitorArea.Right - monitorArea.Left;
            var monitorHeight = monitorArea.Bottom - monitorArea.Top;
            if (width <= 0 || height <= 0 || monitorWidth <= 0 || monitorHeight <= 0)
            {
                return true;
            }

            foundEdge = width >= height
                ? (windowArea.Top - monitorArea.Top <= monitorArea.Bottom - windowArea.Bottom
                    ? TaskbarEdge.Top
                    : TaskbarEdge.Bottom)
                : (windowArea.Left - monitorArea.Left <= monitorArea.Right - windowArea.Right
                    ? TaskbarEdge.Left
                    : TaskbarEdge.Right);
            return false;
        }, IntPtr.Zero);

        edge = foundEdge.GetValueOrDefault();
        return foundEdge.HasValue;
    }

    private static bool IsTaskbarWindow(IntPtr window)
    {
        var className = new System.Text.StringBuilder(128);
        var length = GetClassName(window, className, className.Capacity);
        return length > 0
            && (className.ToString() == "Shell_TrayWnd"
                || className.ToString() == "Shell_SecondaryTrayWnd");
    }

    private static ScreenRect ToScreenRect(NativeRect rect) =>
        new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr window, System.Text.StringBuilder className, int maxCount);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(uint message, ref AppBarData data);

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int Size;
        public IntPtr Window;
        public uint CallbackMessage;
        public uint Edge;
        public NativeRect Rect;
        public IntPtr Parameter;
    }
}

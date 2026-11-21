using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace Wessl.App.Services;

internal static class TrayPlacement
{
    private const uint MonitorDefaultToNearest = 2;
    private const int TraySpacing = 12;

    public static PointInt32 GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PointInt32(point.X, point.Y);
    }

    public static void MoveMenuHost(AppWindow window, PointInt32 cursorPosition) =>
        window.Move(cursorPosition);

    public static void MoveWindowNearTray(AppWindow window, PointInt32 cursorPosition)
    {
        var monitor = MonitorFromPoint(
            new NativePoint { X = cursorPosition.X, Y = cursorPosition.Y },
            MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var size = window.Size;
        var workArea = monitorInfo.WorkArea;
        var x = cursorPosition.X + TraySpacing;
        var y = cursorPosition.Y - size.Height - TraySpacing;

        if (x + size.Width > workArea.Right)
        {
            x = cursorPosition.X - size.Width - TraySpacing;
        }

        if (y < workArea.Top)
        {
            y = cursorPosition.Y + TraySpacing;
        }

        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - size.Width));
        y = Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - size.Height));
        window.Move(new PointInt32(x, y));
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

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
}

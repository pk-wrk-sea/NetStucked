using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NetStucked.Desktop.Behaviors;

public static class WindowWorkArea
{
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(Hook);
    }
    private static IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x24) return IntPtr.Zero; // WM_GETMINMAXINFO uses physical pixels on the window's current monitor.
        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor))
        {
            info.MaxPosition = new(monitor.Work.Left - monitor.Monitor.Left, monitor.Work.Top - monitor.Monitor.Top);
            info.MaxSize = new(monitor.Work.Right - monitor.Work.Left, monitor.Work.Bottom - monitor.Work.Top);
            info.MaxTrackSize = info.MaxSize;
            Marshal.StructureToPtr(info, lParam, false); handled = true;
        }
        return IntPtr.Zero;
    }
    public static bool IsWithinWorkArea(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref monitor) && GetWindowRect(hwnd, out var rect)
            && rect.Left >= monitor.Work.Left && rect.Top >= monitor.Work.Top && rect.Right <= monitor.Work.Right && rect.Bottom <= monitor.Work.Bottom;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point(int x, int y) { public int X = x; public int Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
}

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace NetStucked.Desktop.Services;

/// <summary>Give native taskbar/Alt-Tab surfaces the supplied brand at both Windows icon sizes.</summary>
public static class WindowBranding
{
    public const string ApplicationId = "NetStucked.Desktop";
    public static void SetApplicationIdentity() => Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(ApplicationId));
    public static void Attach(Window window)
    {
        IntPtr small = IntPtr.Zero, large = IntPtr.Zero;
        window.SourceInitialized += (_, _) =>
        {
            using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/NetStucked;component/Resources/Brand/NetStucked.ico")).Stream;
            using var memory = new System.IO.MemoryStream(); stream.CopyTo(memory); byte[] ico = memory.ToArray();
            small = Create(ico, 16); large = Create(ico, 32);
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            _ = SendMessage(hwnd, 0x80, IntPtr.Zero, small);
            _ = SendMessage(hwnd, 0x80, new IntPtr(1), large);
        };
        window.Closed += (_, _) => { if (small != IntPtr.Zero) DestroyIcon(small); if (large != IntPtr.Zero) DestroyIcon(large); };
    }
    private static IntPtr Create(byte[] ico, int size)
    {
        int count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4)), best = 0, distance = int.MaxValue;
        for (int i = 0; i < count; i++) { int width = ico[6 + i * 16] == 0 ? 256 : ico[6 + i * 16]; int delta = Math.Abs(width - size); if (delta < distance) { distance = delta; best = i; } }
        int entry = 6 + best * 16, length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(entry + 8))), offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(entry + 12)));
        byte[] image = ico.AsSpan(offset, length).ToArray(); var icon = CreateIconFromResourceEx(image, (uint)image.Length, true, 0x00030000, size, size, 0);
        if (icon == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not load the NetStucked window icon."); return icon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string id);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr CreateIconFromResourceEx(byte[] bits, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
}

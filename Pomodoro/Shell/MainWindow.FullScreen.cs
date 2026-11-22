using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Pomodoro.Shell;

public partial class MainWindow
{
    private bool isFullScreen;
    private WindowState stateBeforeFullScreen;
    private WindowStyle styleBeforeFullScreen;
    private ResizeMode resizeBeforeFullScreen;
    private Rect boundsBeforeFullScreen;
    private double minWidthBeforeFullScreen;
    private double minHeightBeforeFullScreen;

    private void ConfigureFullScreen()
    {
        DpiChanged += (_, _) =>
        {
            if (!isFullScreen || Dispatcher.HasShutdownStarted) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (isFullScreen && TryGetFullScreenBounds(out var bounds)) PositionFullScreen(bounds);
            }));
        };
    }

    private void ToggleFullScreen()
    {
        if (isFullScreen) { ExitFullScreen(); return; }
        if (!TryGetFullScreenBounds(out var monitorBounds)) return;

        stateBeforeFullScreen = WindowState;
        styleBeforeFullScreen = WindowStyle;
        resizeBeforeFullScreen = ResizeMode;
        boundsBeforeFullScreen = RestoreBounds;
        minWidthBeforeFullScreen = MinWidth;
        minHeightBeforeFullScreen = MinHeight;

        // Use the whole current monitor, including the taskbar area. Keep the
        // original normal-window bounds so leaving fullscreen restores them.
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        MinWidth = MinHeight = 0;
        isFullScreen = true;
        if (!PositionFullScreen(monitorBounds))
        {
            ExitFullScreen();
            MessageBox.Show(this, "Chưa thể chuyển sang toàn màn hình. Hãy thử lại.", "Pomodoro",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExitFullScreen()
    {
        isFullScreen = false;
        WindowState = WindowState.Normal;
        WindowStyle = styleBeforeFullScreen;
        ResizeMode = resizeBeforeFullScreen;
        MinWidth = minWidthBeforeFullScreen;
        MinHeight = minHeightBeforeFullScreen;
        if (!boundsBeforeFullScreen.IsEmpty)
        {
            Left = boundsBeforeFullScreen.Left;
            Top = boundsBeforeFullScreen.Top;
            Width = boundsBeforeFullScreen.Width;
            Height = boundsBeforeFullScreen.Height;
        }
        WindowState = stateBeforeFullScreen;
    }

    private bool TryGetFullScreenBounds(out FullScreenRect bounds)
    {
        bounds = default;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return false;
        var monitor = MonitorFromWindow(handle, 2); // MONITOR_DEFAULTTONEAREST
        var info = new FullScreenMonitorInfo { Size = (uint)Marshal.SizeOf<FullScreenMonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return false;
        bounds = info.Monitor;
        return bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
    }

    private bool PositionFullScreen(FullScreenRect bounds)
    {
        // Native monitor coordinates and SetWindowPos share the same DPI space.
        // Avoid changing activation/Z-order so Alt+Tab keeps working normally.
        const uint flags = 0x0004 | 0x0010 | 0x0020; // NOZORDER | NOACTIVATE | FRAMECHANGED
        return SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero,
            bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, flags);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FullScreenRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct FullScreenMonitorInfo
    {
        public uint Size;
        public FullScreenRect Monitor, WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref FullScreenMonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

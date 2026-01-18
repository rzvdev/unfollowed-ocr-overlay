using System.Runtime.InteropServices;

namespace Unfollowed.Capture;

public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

public interface IWin32CursorApi
{
    IntPtr GetDC(IntPtr hWnd);
    int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    bool GetCursorPos(out Win32Point lpPoint);
    bool GetPhysicalCursorPos(out Win32Point lpPoint);
    short GetAsyncKeyState(int vKey);
    bool DrawFocusRect(IntPtr hdc, ref Win32Rect lprc);
    IntPtr MonitorFromPoint(Win32Point pt, uint dwFlags);
    bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);
}

public sealed class Win32RoiSelector : IRoiSelector
{
    private const int VK_LBUTTON = 0x01;
    private const int VK_ESCAPE = 0x1B;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private readonly IWin32CursorApi _cursorApi;

    public Win32RoiSelector(IWin32CursorApi? cursorApi = null)
    {
        _cursorApi = cursorApi ?? new Win32CursorApi();
    }

    public async Task<RoiSelection> SelectRegionAsync(CancellationToken ct)
    {
        Console.WriteLine("Click and drag to select the ROI. Press ESC to cancel.");

        var screenDc = _cursorApi.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to acquire screen DC for ROI selection.");
        }

        try
        {
            var start = await WaitForMouseDownAsync(ct);
            var startMonitor = _cursorApi.MonitorFromPoint(start, MONITOR_DEFAULTTONEAREST);
            var monitorId = GetMonitorIndex(startMonitor);
            var previousRect = default(Win32Rect);
            var hasPrevious = false;

            while (IsButtonDown(VK_LBUTTON))
            {
                ct.ThrowIfCancellationRequested();
                if (IsButtonDown(VK_ESCAPE))
                {
                    throw new TaskCanceledException("ROI selection cancelled.");
                }

                if (!TryGetPhysicalCursorPos(out var current))
                {
                    await Task.Delay(16, ct);
                    continue;
                }

                var rect = NormalizeRect(start, current);
                if (hasPrevious && !rect.Equals(previousRect))
                {
                    _cursorApi.DrawFocusRect(screenDc, ref previousRect);
                    _cursorApi.DrawFocusRect(screenDc, ref rect);
                    previousRect = rect;
                }
                else if (!hasPrevious)
                {
                    _cursorApi.DrawFocusRect(screenDc, ref rect);
                    previousRect = rect;
                    hasPrevious = true;
                }

                await Task.Delay(16, ct);
            }

            if (hasPrevious)
            {
                _cursorApi.DrawFocusRect(screenDc, ref previousRect);
            }

            if (!TryGetPhysicalCursorPos(out var end))
            {
                end = start;
            }

            var finalRect = NormalizeRect(start, end);
            var width = Math.Max(1, finalRect.Right - finalRect.Left);
            var height = Math.Max(1, finalRect.Bottom - finalRect.Top);

            return new RoiSelection(finalRect.Left, finalRect.Top, width, height, monitorId);
        }
        finally
        {
            _cursorApi.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private async Task<Win32Point> WaitForMouseDownAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (IsButtonDown(VK_ESCAPE))
            {
                throw new TaskCanceledException("ROI selection cancelled.");
            }

            if (IsButtonDown(VK_LBUTTON) && TryGetPhysicalCursorPos(out var start))
            {
                return start;
            }

            await Task.Delay(16, ct);
        }
    }

    private bool IsButtonDown(int key)
        => (_cursorApi.GetAsyncKeyState(key) & 0x8000) != 0;

    private bool TryGetPhysicalCursorPos(out Win32Point point)
    {
        if (_cursorApi.GetPhysicalCursorPos(out point))
        {
            return true;
        }

        return _cursorApi.GetCursorPos(out point);
    }

    private static Win32Rect NormalizeRect(Win32Point start, Win32Point end)
    {
        var left = Math.Min(start.X, end.X);
        var right = Math.Max(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var bottom = Math.Max(start.Y, end.Y);

        return new Win32Rect
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom
        };
    }

    private int GetMonitorIndex(IntPtr monitor)
    {
        var index = 0;
        var found = -1;

        _cursorApi.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            if (hMonitor == monitor)
            {
                found = index;
                return false;
            }

            index++;
            return true;
        }, IntPtr.Zero);

        return found < 0 ? 0 : found;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Win32Point
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
public struct Win32Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

public sealed class Win32CursorApi : IWin32CursorApi
{
    public IntPtr GetDC(IntPtr hWnd) => NativeMethods.GetDC(hWnd);

    public int ReleaseDC(IntPtr hWnd, IntPtr hDC) => NativeMethods.ReleaseDC(hWnd, hDC);

    public bool GetCursorPos(out Win32Point lpPoint) => NativeMethods.GetCursorPos(out lpPoint);

    public bool GetPhysicalCursorPos(out Win32Point lpPoint) => NativeMethods.GetPhysicalCursorPos(out lpPoint);

    public short GetAsyncKeyState(int vKey) => NativeMethods.GetAsyncKeyState(vKey);

    public bool DrawFocusRect(IntPtr hdc, ref Win32Rect lprc) => NativeMethods.DrawFocusRect(hdc, ref lprc);

    public IntPtr MonitorFromPoint(Win32Point pt, uint dwFlags) => NativeMethods.MonitorFromPoint(pt, dwFlags);

    public bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData)
        => NativeMethods.EnumDisplayMonitors(hdc, lprcClip, lpfnEnum, dwData);

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Win32Point lpPoint);

        [DllImport("user32.dll")]
        public static extern bool GetPhysicalCursorPos(out Win32Point lpPoint);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern bool DrawFocusRect(IntPtr hdc, ref Win32Rect lprc);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(Win32Point pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData);
    }
}

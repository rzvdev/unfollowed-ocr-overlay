using System.Runtime.InteropServices;

namespace Unfollowed.Capture;

public sealed class Win32RoiSelector : IRoiSelector
{
    private const int VK_LBUTTON = 0x01;
    private const int VK_ESCAPE = 0x1B;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    public async Task<RoiSelection> SelectRegionAsync(CancellationToken ct)
    {
        Console.WriteLine("Click and drag to select the ROI. Press ESC to cancel.");

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to acquire screen DC for ROI selection.");
        }

        try
        {
            var start = await WaitForMouseDownAsync(ct);
            var startMonitor = MonitorFromPoint(start, MONITOR_DEFAULTTONEAREST);
            var monitorId = GetMonitorIndex(startMonitor);
            var previousRect = default(RECT);
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
                    DrawFocusRect(screenDc, ref previousRect);
                    DrawFocusRect(screenDc, ref rect);
                    previousRect = rect;
                }
                else if (!hasPrevious)
                {
                    DrawFocusRect(screenDc, ref rect);
                    previousRect = rect;
                    hasPrevious = true;
                }

                await Task.Delay(16, ct);
            }

            if (hasPrevious)
            {
                DrawFocusRect(screenDc, ref previousRect);
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
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static async Task<POINT> WaitForMouseDownAsync(CancellationToken ct)
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

    private static bool IsButtonDown(int key)
        => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool TryGetPhysicalCursorPos(out POINT point)
    {
        if (GetPhysicalCursorPos(out point))
        {
            return true;
        }

        return GetCursorPos(out point);
    }

    private static RECT NormalizeRect(POINT start, POINT end)
    {
        var left = Math.Min(start.X, end.X);
        var right = Math.Max(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var bottom = Math.Max(start.Y, end.Y);

        return new RECT
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetPhysicalCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool DrawFocusRect(IntPtr hdc, ref RECT lprc);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    private static int GetMonitorIndex(IntPtr monitor)
    {
        var index = 0;
        var found = -1;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
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

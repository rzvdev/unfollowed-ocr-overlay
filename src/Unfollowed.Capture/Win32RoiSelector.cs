using System.Runtime.InteropServices;

namespace Unfollowed.Capture;

public sealed class Win32RoiSelector : IRoiSelector
{
    private const int VK_LBUTTON = 0x01;
    private const int VK_ESCAPE = 0x1B;

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
            var previousRect = default(RECT);
            var hasPrevious = false;

            while (IsButtonDown(VK_LBUTTON))
            {
                ct.ThrowIfCancellationRequested();
                if (IsButtonDown(VK_ESCAPE))
                {
                    throw new TaskCanceledException("ROI selection cancelled.");
                }

                if (!TryGetCursorPos(out var current))
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

            if (!TryGetCursorPos(out var end))
            {
                end = start;
            }

            var finalRect = NormalizeRect(start, end);
            var width = Math.Max(1, finalRect.Right - finalRect.Left);
            var height = Math.Max(1, finalRect.Bottom - finalRect.Top);

            return new RoiSelection(finalRect.Left, finalRect.Top, width, height);
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

            if (IsButtonDown(VK_LBUTTON) && TryGetCursorPos(out var start))
            {
                return start;
            }

            await Task.Delay(16, ct);
        }
    }

    private static bool IsButtonDown(int key)
        => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool TryGetCursorPos(out POINT point)
        => GetCursorPos(out point);

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
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool DrawFocusRect(IntPtr hdc, ref RECT lprc);
}

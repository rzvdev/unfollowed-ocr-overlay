using System.Runtime.InteropServices;

namespace Unfollowed.Capture;

public interface IWin32ScreenApi
{
    IntPtr GetDC(IntPtr hWnd);
    int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    IntPtr CreateCompatibleDC(IntPtr hdc);
    bool DeleteDC(IntPtr hdc);
    IntPtr CreateDIBSection(
        IntPtr hdc,
        ref Win32BitmapInfo pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);
    IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    bool DeleteObject(IntPtr hObject);
    bool BitBlt(
        IntPtr hdcDest,
        int nXDest,
        int nYDest,
        int nWidth,
        int nHeight,
        IntPtr hdcSrc,
        int nXSrc,
        int nYSrc,
        uint dwRop);
}

public sealed class Win32FrameCapture : IFrameCapture
{
    private const int BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;

    private readonly IWin32ScreenApi _screenApi;
    private IntPtr _screenDc;
    private IntPtr _memoryDc;
    private IntPtr _dib;
    private IntPtr _oldBitmap;
    private IntPtr _bits;
    private RoiSelection? _roi;
    private int _width;
    private int _height;
    private byte[]? _buffer;
    private bool _initialized;

    public Win32FrameCapture(IWin32ScreenApi? screenApi = null)
    {
        _screenApi = screenApi ?? new Win32ScreenApi();
    }

    public Task InitializeAsync(RoiSelection roi, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (roi.Width <= 0 || roi.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(roi), "ROI must have non-zero dimensions.");
        }

        _roi = roi;
        _width = roi.Width;
        _height = roi.Height;

        _screenDc = _screenApi.GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to acquire screen DC.");
        }

        _memoryDc = _screenApi.CreateCompatibleDC(_screenDc);
        if (_memoryDc == IntPtr.Zero)
        {
            _screenApi.ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
            throw new InvalidOperationException("Failed to create memory DC.");
        }

        var bitmapInfo = new Win32BitmapInfo
        {
            bmiHeader = new Win32BitmapInfoHeader
            {
                biSize = (uint)Marshal.SizeOf<Win32BitmapInfoHeader>(),
                biWidth = _width,
                biHeight = -_height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
                biSizeImage = (uint)(_width * _height * 4)
            },
            bmiColors = new uint[1]
        };

        _dib = _screenApi.CreateDIBSection(_screenDc, ref bitmapInfo, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero || _bits == IntPtr.Zero)
        {
            CleanupHandles();
            throw new InvalidOperationException("Failed to create DIB section.");
        }

        _oldBitmap = _screenApi.SelectObject(_memoryDc, _dib);
        if (_oldBitmap == IntPtr.Zero)
        {
            CleanupHandles();
            throw new InvalidOperationException("Failed to select bitmap into DC.");
        }

        _buffer = new byte[_width * _height * 4];
        _initialized = true;

        return Task.CompletedTask;
    }

    public Task<CaptureFrame> CaptureAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!_initialized || _roi is null || _buffer is null)
        {
            throw new InvalidOperationException("Capture has not been initialized.");
        }

        var success = _screenApi.BitBlt(
            _memoryDc,
            0,
            0,
            _width,
            _height,
            _screenDc,
            _roi.X,
            _roi.Y,
            SRCCOPY | CAPTUREBLT);

        if (!success)
        {
            throw new InvalidOperationException("BitBlt failed to capture the frame.");
        }

        Marshal.Copy(_bits, _buffer, 0, _buffer.Length);

        var frame = new CaptureFrame(
            _buffer.ToArray(),
            _width,
            _height,
            DateTime.UtcNow.Ticks);

        return Task.FromResult(frame);
    }

    public ValueTask DisposeAsync()
    {
        CleanupHandles();
        _initialized = false;
        _buffer = null;
        _roi = null;

        return ValueTask.CompletedTask;
    }

    private void CleanupHandles()
    {
        if (_memoryDc != IntPtr.Zero && _oldBitmap != IntPtr.Zero)
        {
            _screenApi.SelectObject(_memoryDc, _oldBitmap);
            _oldBitmap = IntPtr.Zero;
        }

        if (_dib != IntPtr.Zero)
        {
            _screenApi.DeleteObject(_dib);
            _dib = IntPtr.Zero;
        }

        if (_memoryDc != IntPtr.Zero)
        {
            _screenApi.DeleteDC(_memoryDc);
            _memoryDc = IntPtr.Zero;
        }

        if (_screenDc != IntPtr.Zero)
        {
            _screenApi.ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct Win32BitmapInfo
{
    public Win32BitmapInfoHeader bmiHeader;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
    public uint[] bmiColors;
}

[StructLayout(LayoutKind.Sequential)]
public struct Win32BitmapInfoHeader
{
    public uint biSize;
    public int biWidth;
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public int biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;
}

public sealed class Win32ScreenApi : IWin32ScreenApi
{
    public IntPtr GetDC(IntPtr hWnd) => NativeMethods.GetDC(hWnd);

    public int ReleaseDC(IntPtr hWnd, IntPtr hDC) => NativeMethods.ReleaseDC(hWnd, hDC);

    public IntPtr CreateCompatibleDC(IntPtr hdc) => NativeMethods.CreateCompatibleDC(hdc);

    public bool DeleteDC(IntPtr hdc) => NativeMethods.DeleteDC(hdc);

    public IntPtr CreateDIBSection(
        IntPtr hdc,
        ref Win32BitmapInfo pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset)
        => NativeMethods.CreateDIBSection(hdc, ref pbmi, iUsage, out ppvBits, hSection, dwOffset);

    public IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj) => NativeMethods.SelectObject(hdc, hgdiobj);

    public bool DeleteObject(IntPtr hObject) => NativeMethods.DeleteObject(hObject);

    public bool BitBlt(
        IntPtr hdcDest,
        int nXDest,
        int nYDest,
        int nWidth,
        int nHeight,
        IntPtr hdcSrc,
        int nXSrc,
        int nYSrc,
        uint dwRop)
        => NativeMethods.BitBlt(hdcDest, nXDest, nYDest, nWidth, nHeight, hdcSrc, nXSrc, nYSrc, dwRop);

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(
            IntPtr hdc,
            ref Win32BitmapInfo pbmi,
            uint iUsage,
            out IntPtr ppvBits,
            IntPtr hSection,
            uint dwOffset);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(
            IntPtr hdcDest,
            int nXDest,
            int nYDest,
            int nWidth,
            int nHeight,
            IntPtr hdcSrc,
            int nXSrc,
            int nYSrc,
            uint dwRop);
    }
}

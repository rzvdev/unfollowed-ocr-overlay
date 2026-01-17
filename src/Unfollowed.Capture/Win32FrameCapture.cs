using System.Runtime.InteropServices;

namespace Unfollowed.Capture;

public sealed class Win32FrameCapture : IFrameCapture
{
    private const int BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;

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

        _screenDc = GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to acquire screen DC.");
        }

        _memoryDc = CreateCompatibleDC(_screenDc);
        if (_memoryDc == IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
            throw new InvalidOperationException("Failed to create memory DC.");
        }

        var bitmapInfo = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = _width,
                biHeight = -_height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
                biSizeImage = (uint)(_width * _height * 4)
            },
            bmiColors = new uint[1]
        };

        _dib = CreateDIBSection(_screenDc, ref bitmapInfo, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero || _bits == IntPtr.Zero)
        {
            CleanupHandles();
            throw new InvalidOperationException("Failed to create DIB section.");
        }

        _oldBitmap = SelectObject(_memoryDc, _dib);
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

        var success = BitBlt(
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
            SelectObject(_memoryDc, _oldBitmap);
            _oldBitmap = IntPtr.Zero;
        }

        if (_dib != IntPtr.Zero)
        {
            DeleteObject(_dib);
            _dib = IntPtr.Zero;
        }

        if (_memoryDc != IntPtr.Zero)
        {
            DeleteDC(_memoryDc);
            _memoryDc = IntPtr.Zero;
        }

        if (_screenDc != IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public uint[] bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BITMAPINFO pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
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

using Unfollowed.Capture;
using Unfollowed.Ocr;
using Unfollowed.Preprocess;
using Windows.Media.Ocr;

namespace Unfollowed.App.Tests;

public sealed class Win32InteropFailureTests
{
    [Fact]
    public async Task Win32FrameCapture_Throws_WhenScreenDcUnavailable()
    {
        var api = new FakeWin32ScreenApi
        {
            ScreenDc = IntPtr.Zero
        };
        var capture = new Win32FrameCapture(api);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.InitializeAsync(
            new RoiSelection(0, 0, 10, 10),
            CancellationToken.None));

        Assert.Equal("Failed to acquire screen DC.", ex.Message);
    }

    [Fact]
    public async Task Win32FrameCapture_CleansUp_WhenDibSectionCreationFails()
    {
        var api = new FakeWin32ScreenApi
        {
            ScreenDc = new IntPtr(1),
            MemoryDc = new IntPtr(2),
            DibSection = IntPtr.Zero,
            Bits = IntPtr.Zero
        };
        var capture = new Win32FrameCapture(api);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.InitializeAsync(
            new RoiSelection(0, 0, 10, 10),
            CancellationToken.None));

        Assert.Equal("Failed to create DIB section.", ex.Message);
        Assert.True(api.DeleteDcCalled);
        Assert.True(api.ReleaseDcCalled);
    }

    [Fact]
    public async Task Win32RoiSelector_Throws_WhenScreenDcUnavailable()
    {
        var api = new FakeWin32CursorApi
        {
            ScreenDc = IntPtr.Zero
        };
        var selector = new Win32RoiSelector(api);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => selector.SelectRegionAsync(CancellationToken.None));

        Assert.Equal("Failed to acquire screen DC for ROI selection.", ex.Message);
    }

    [Fact]
    public async Task WindowsOcrProvider_Throws_WhenEngineUnavailable()
    {
        var provider = new WindowsOcrProvider(new FakeOcrEngineFactory());
        var frame = new ProcessedFrame(new byte[4], 2, 2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.RecognizeAsync(
            frame,
            new OcrOptions(),
            CancellationToken.None));

        Assert.Equal("Windows OCR engine could not be created.", ex.Message);
    }

    private sealed class FakeWin32ScreenApi : IWin32ScreenApi
    {
        public IntPtr ScreenDc { get; set; } = new IntPtr(1);
        public IntPtr MemoryDc { get; set; } = new IntPtr(2);
        public IntPtr DibSection { get; set; } = new IntPtr(3);
        public IntPtr Bits { get; set; } = new IntPtr(4);
        public bool DeleteDcCalled { get; private set; }
        public bool ReleaseDcCalled { get; private set; }

        public IntPtr GetDC(IntPtr hWnd) => ScreenDc;

        public int ReleaseDC(IntPtr hWnd, IntPtr hDC)
        {
            if (hDC == ScreenDc)
            {
                ReleaseDcCalled = true;
            }
            return 1;
        }

        public IntPtr CreateCompatibleDC(IntPtr hdc) => MemoryDc;

        public bool DeleteDC(IntPtr hdc)
        {
            if (hdc == MemoryDc)
            {
                DeleteDcCalled = true;
            }
            return true;
        }

        public IntPtr CreateDIBSection(
            IntPtr hdc,
            ref Win32BitmapInfo pbmi,
            uint iUsage,
            out IntPtr ppvBits,
            IntPtr hSection,
            uint dwOffset)
        {
            ppvBits = Bits;
            return DibSection;
        }

        public IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj) => new IntPtr(5);

        public bool DeleteObject(IntPtr hObject) => true;

        public bool BitBlt(
            IntPtr hdcDest,
            int nXDest,
            int nYDest,
            int nWidth,
            int nHeight,
            IntPtr hdcSrc,
            int nXSrc,
            int nYSrc,
            uint dwRop) => true;
    }

    private sealed class FakeWin32CursorApi : IWin32CursorApi
    {
        public IntPtr ScreenDc { get; set; } = new IntPtr(1);

        public IntPtr GetDC(IntPtr hWnd) => ScreenDc;

        public int ReleaseDC(IntPtr hWnd, IntPtr hDC) => 1;

        public bool GetCursorPos(out Win32Point lpPoint)
        {
            lpPoint = default;
            return false;
        }

        public bool GetPhysicalCursorPos(out Win32Point lpPoint)
        {
            lpPoint = default;
            return false;
        }

        public short GetAsyncKeyState(int vKey) => 0;

        public bool DrawFocusRect(IntPtr hdc, ref Win32Rect lprc) => true;

        public IntPtr MonitorFromPoint(Win32Point pt, uint dwFlags) => IntPtr.Zero;

        public bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData) => true;
    }

    private sealed class FakeOcrEngineFactory : IWindowsOcrEngineFactory
    {
        public OcrEngine? CreateEngine(OcrOptions options) => null;
    }
}

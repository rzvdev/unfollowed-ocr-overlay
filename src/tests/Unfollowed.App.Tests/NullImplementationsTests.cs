using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Tests;

public sealed class NullImplementationsTests
{
    [Fact]
    public async Task NullFrameCapture_InitializeAndDisposeComplete()
    {
        var capture = new NullFrameCapture();
        var roi = new RoiSelection(0, 0, 100, 50);

        var initializeException = await Record.ExceptionAsync(() => capture.InitializeAsync(roi, CancellationToken.None));
        var disposeException = await Record.ExceptionAsync(async () => await capture.DisposeAsync());

        Assert.Null(initializeException);
        Assert.Null(disposeException);
    }

    [Fact]
    public async Task NullFrameCapture_CaptureThrowsNotSupported()
    {
        var capture = new NullFrameCapture();

        await Assert.ThrowsAsync<NotSupportedException>(() => capture.CaptureAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NullOverlayRenderer_DoesNotThrow()
    {
        var renderer = new NullOverlayRenderer();
        var roi = new RoiSelection(0, 0, 200, 100);
        var options = new OverlayOptions();
        var highlights = Array.Empty<Highlight>();

        var initializeException = await Record.ExceptionAsync(() => renderer.InitializeAsync(roi, options, CancellationToken.None));
        var renderException = await Record.ExceptionAsync(() => renderer.RenderAsync(highlights, CancellationToken.None));
        var clearException = await Record.ExceptionAsync(() => renderer.ClearAsync(CancellationToken.None));
        var disposeException = await Record.ExceptionAsync(async () => await renderer.DisposeAsync());

        Assert.Null(initializeException);
        Assert.Null(renderException);
        Assert.Null(clearException);
        Assert.Null(disposeException);
    }

    [Fact]
    public async Task NullOcrProvider_ReturnsEmptyTokensAndPreservesDimensions()
    {
        var provider = new NullOcrProvider();
        var frame = new ProcessedFrame(Array.Empty<byte>(), 320, 180);
        var options = new OcrOptions();

        var result = await provider.RecognizeAsync(frame, options, CancellationToken.None);

        Assert.Empty(result.Tokens);
        Assert.Equal(frame.Width, result.FrameWidth);
        Assert.Equal(frame.Height, result.FrameHeight);
    }

    [Fact]
    public void NoOpFramePreprocessor_ReturnsEmptyBufferAndPreservesDimensions()
    {
        var preprocessor = new NoOpFramePreprocessor();
        var frame = new CaptureFrame(new byte[] { 1, 2, 3 }, 640, 480, 1234);
        var options = new PreprocessOptions();

        var result = preprocessor.Process(frame, options);

        Assert.Empty(result.Gray8);
        Assert.Equal(frame.Width, result.Width);
        Assert.Equal(frame.Height, result.Height);
    }
}

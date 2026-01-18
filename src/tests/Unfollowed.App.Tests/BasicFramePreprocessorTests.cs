using Unfollowed.Capture;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Tests;

public class BasicFramePreprocessorTests
{
    [Fact]
    public void Process_ThrowsWhenBufferTooSmall()
    {
        var frame = new CaptureFrame(new byte[15], 2, 2, 0);
        var preprocessor = new BasicFramePreprocessor();

        Assert.Throws<InvalidOperationException>(() => preprocessor.Process(frame, new PreprocessOptions()));
    }

    [Fact]
    public void Process_ContrastOne_NoChangeToGray()
    {
        var frame = CreateFrameFromGray(new byte[] { 123 }, 1, 1);
        var preprocessor = new BasicFramePreprocessor();

        var processed = preprocessor.Process(frame, new PreprocessOptions(Contrast: 1.0f));

        Assert.Equal(new byte[] { 123 }, processed.Gray8);
    }

    [Fact]
    public void Process_ContrastHigher_AdjustsValue()
    {
        var frame = CreateFrameFromGray(new byte[] { 100 }, 1, 1);
        var preprocessor = new BasicFramePreprocessor();

        var processed = preprocessor.Process(frame, new PreprocessOptions(Contrast: 2.0f));

        Assert.Equal(new byte[] { 72 }, processed.Gray8);
    }

    [Fact]
    public void Process_HighContrastThresholdsAt128()
    {
        var frame = CreateFrameFromGray(new byte[] { 127, 128 }, 2, 1);
        var preprocessor = new BasicFramePreprocessor();

        var processed = preprocessor.Process(frame, new PreprocessOptions(Profile: PreprocessProfile.HighContrast));

        Assert.Equal(new byte[] { 0, 255 }, processed.Gray8);
    }

    [Fact]
    public void Process_SharpenedOutputMatchesExpected()
    {
        var source = new byte[]
        {
            10, 20, 30,
            40, 50, 60,
            70, 80, 90
        };
        var frame = CreateFrameFromGray(source, 3, 3);
        var preprocessor = new BasicFramePreprocessor();

        var processed = preprocessor.Process(frame, new PreprocessOptions(Sharpen: 1.0f));

        var expected = new byte[]
        {
            0, 5, 20,
            35, 50, 65,
            80, 95, 110
        };

        Assert.Equal(3, processed.Width);
        Assert.Equal(3, processed.Height);
        Assert.Equal(expected, processed.Gray8);
    }

    private static CaptureFrame CreateFrameFromGray(byte[] gray, int width, int height)
    {
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < gray.Length; i++)
        {
            var offset = i * 4;
            var value = gray[i];
            bgra[offset] = value;
            bgra[offset + 1] = value;
            bgra[offset + 2] = value;
            bgra[offset + 3] = 255;
        }

        return new CaptureFrame(bgra, width, height, 0);
    }
}

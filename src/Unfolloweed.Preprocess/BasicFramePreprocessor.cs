using Unfollowed.Capture;

namespace Unfollowed.Preprocess;

public sealed class BasicFramePreprocessor : IFramePreprocessor
{
    public ProcessedFrame Process(CaptureFrame frame, PreprocessOptions options)
    {
        var pixelCount = frame.Width * frame.Height;
        var expectedLength = pixelCount * 4;

        if (frame.Bgra32.Length < expectedLength)
        {
            throw new InvalidOperationException("Frame buffer is smaller than expected.");
        }

        var gray = new byte[pixelCount];
        var contrast = options.Contrast;

        for (var i = 0; i < pixelCount; i++)
        {
            var offset = i * 4;
            var b = frame.Bgra32[offset];
            var g = frame.Bgra32[offset + 1];
            var r = frame.Bgra32[offset + 2];

            var luminance = (r * 77 + g * 150 + b * 29) >> 8;
            var adjusted = ApplyContrast(luminance, contrast);

            if (options.Profile == PreprocessProfile.HighContrast)
            {
                adjusted = adjusted >= 128 ? 255 : 0;
            }

            gray[i] = (byte)adjusted;
        }

        return new ProcessedFrame(gray, frame.Width, frame.Height);
    }

    private static int ApplyContrast(int value, float contrast)
    {
        if (Math.Abs(contrast - 1.0f) < 0.0001f)
        {
            return value;
        }

        var adjusted = (value - 128f) * contrast + 128f;
        return ClampToByte(adjusted);
    }

    private static int ClampToByte(float value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 255)
        {
            return 255;
        }

        return (int)Math.Round(value);
    }
}

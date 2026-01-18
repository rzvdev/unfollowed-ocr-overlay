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

        if (options.Sharpen > 0.001f)
        {
            var amount = Math.Clamp(options.Sharpen, 0.0f, 2.0f);
            gray = ApplyUnsharpMask(gray, frame.Width, frame.Height, amount);
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

    private static byte[] ApplyUnsharpMask(byte[] source, int width, int height, float amount)
    {
        var blurred = new byte[source.Length];
        var output = new byte[source.Length];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var sum = 0;
                var count = 0;

                for (var ky = -1; ky <= 1; ky++)
                {
                    var ny = y + ky;
                    if (ny < 0 || ny >= height)
                        continue;

                    var nOffset = ny * width;
                    for (var kx = -1; kx <= 1; kx++)
                    {
                        var nx = x + kx;
                        if (nx < 0 || nx >= width)
                            continue;

                        sum += source[nOffset + nx];
                        count++;
                    }
                }

                blurred[rowOffset + x] = (byte)(sum / count);
            }
        }

        for (var i = 0; i < source.Length; i++)
        {
            var sharpened = source[i] + (source[i] - blurred[i]) * amount;
            output[i] = (byte)ClampToByte(sharpened);
        }

        return output;
    }
}

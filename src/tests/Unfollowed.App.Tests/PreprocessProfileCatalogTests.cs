using Unfollowed.Capture;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Tests;

public class PreprocessProfileCatalogTests
{
    [Fact]
    public void Resolve_SelectedProfileDrivesPreprocessingOutput()
    {
        var profiles = new Dictionary<string, PreprocessOptions>
        {
            ["High"] = new(Profile: PreprocessProfile.HighContrast)
        };
        var fallback = new PreprocessOptions();

        var resolved = PreprocessProfileCatalog.Resolve("High", profiles, fallback);

        var frame = CreateFrameFromGray(new byte[] { 127, 128 }, 2, 1);
        var preprocessor = new BasicFramePreprocessor();

        var processed = preprocessor.Process(frame, resolved);

        Assert.Equal(new byte[] { 0, 255 }, processed.Gray8);
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

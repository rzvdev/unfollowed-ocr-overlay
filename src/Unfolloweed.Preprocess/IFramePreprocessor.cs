using Unfollowed.Capture;

namespace Unfollowed.Preprocess;

public interface IFramePreprocessor
{
    ProcessedFrame Process(CaptureFrame frame, PreprocessOptions options);
}

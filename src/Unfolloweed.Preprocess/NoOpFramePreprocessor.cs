using Unfollowed.Capture;

namespace Unfollowed.Preprocess
{
    public sealed class NoOpFramePreprocessor : IFramePreprocessor
    {
        public ProcessedFrame Process(CaptureFrame frame, PreprocessOptions options)
        {
            return new(Array.Empty<byte>(), frame.Width, frame.Height);
        }
    }
}

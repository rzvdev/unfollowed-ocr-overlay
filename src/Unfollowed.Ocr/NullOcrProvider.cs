using Unfollowed.Preprocess;

namespace Unfollowed.Ocr;

public sealed class NullOcrProvider : IOcrProvider
{
    public Task<OcrResult> RecognizeAsync(ProcessedFrame frame, OcrOptions options, CancellationToken ct)
        => Task.FromResult(new OcrResult(Array.Empty<OcrToken>(), frame.Width, frame.Height));
}

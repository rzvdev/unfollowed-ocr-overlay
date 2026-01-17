using Unfollowed.Preprocess;

namespace Unfollowed.Ocr;

public interface IOcrProvider
{
    Task<OcrResult> RecognizeAsync(ProcessedFrame frame, OcrOptions options, CancellationToken ct);
}

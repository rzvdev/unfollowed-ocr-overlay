namespace Unfollowed.Ocr;

public sealed record OcrResult(IReadOnlyList<OcrToken> Tokens, int FrameWidth, int FrameHeight);

using Unfollowed.Core.Models;

namespace Unfollowed.Ocr;

public sealed record OcrToken(string Text, RectF RoiRect, float Confidence);

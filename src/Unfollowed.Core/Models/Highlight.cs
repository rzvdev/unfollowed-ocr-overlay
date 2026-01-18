namespace Unfollowed.Core.Models;

/// <summary>
/// Represents a stabilized highlight in screen coordinates.
/// </summary>
public sealed record Highlight(
   string UsernameNormalized,
   string OcrText,
   float Confidence,
   RectF ScreenRect,
   bool IsCertain
);

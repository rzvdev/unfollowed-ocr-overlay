namespace Unfollowed.Core.Models;

/// <summary>
/// Represents a stabilized highlight in screen coordinates.
/// </summary>
public sealed record Highlight(
   string UsernameNormalized,
   float Confidence,
   RectF ScreenRect,
   bool IsCertain
);

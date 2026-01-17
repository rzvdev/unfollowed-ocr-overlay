namespace Unfollowed.Core.Models;

public sealed record Highlight(
   string UsernameNormalized,
   float Confidence,
   RectF ScreenRect,
   bool IsCertain
);

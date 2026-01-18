namespace Unfollowed.Core.Models;

public sealed record MatchCandidate(
    string UsernameNormalized,
    float Confidence,
    RectF RoiRect
);

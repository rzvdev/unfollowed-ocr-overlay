namespace Unfollowed.Core.Models;

/// <summary>
/// Represents a username candidate extracted from OCR with its confidence and ROI rectangle.
/// </summary>
public sealed record MatchCandidate(
    string UsernameNormalized,
    float Confidence,
    RectF RoiRect
);

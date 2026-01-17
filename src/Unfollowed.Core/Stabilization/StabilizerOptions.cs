namespace Unfollowed.Core.Stabilization;

public sealed record StabilizerOptions(
   int WindowSizeM = 3,
   int RequiredK = 2,
   float ConfidenceThreshold = 0.60f,
   bool AllowUncertainHighlights = false
);

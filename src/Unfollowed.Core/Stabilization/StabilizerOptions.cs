namespace Unfollowed.Core.Stabilization;

/// <summary>
/// Configuration for sliding-window highlight stabilization.
/// </summary>
public sealed record StabilizerOptions(
   /// <summary>
   /// Number of recent frames to retain in the window.
   /// </summary>
   int WindowSizeM = 5,
   /// <summary>
   /// Minimum number of frames a candidate must appear in to be considered stable.
   /// </summary>
   int RequiredK = 3,
   /// <summary>
   /// Minimum confidence for a candidate to participate in stabilization.
   /// </summary>
   float ConfidenceThreshold = 0.70f,
   /// <summary>
   /// When true, includes transient candidates as uncertain highlights.
   /// </summary>
   bool AllowUncertainHighlights = false
);

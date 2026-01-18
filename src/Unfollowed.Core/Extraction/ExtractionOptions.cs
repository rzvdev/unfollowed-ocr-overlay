namespace Unfollowed.Core.Extraction
{
    /// <summary>
    /// Configuration values that shape how OCR tokens are parsed into username candidates.
    /// </summary>
    public sealed record ExtractionOptions(
        /// <summary>
        /// Maximum length accepted for usernames after normalization.
        /// </summary>
        int MaxUsernameLength = 30,
        /// <summary>
        /// Minimum OCR confidence for a token to be considered.
        /// </summary>
        float MinTokenConfidence = 0.0f,
        /// <summary>
        /// Regex pattern used to locate potential usernames in OCR text.
        /// </summary>
        string CandidateReges = "@?[a-zA-Z0-9._]{1,30}",
        /// <summary>
        /// Optional list of stop words that should never be treated as usernames.
        /// </summary>
        IReadOnlyCollection<string>? StopWords = null
    );
}

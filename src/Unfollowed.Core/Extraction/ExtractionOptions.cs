namespace Unfollowed.Core.Extraction
{
    public sealed record ExtractionOptions(
        int MaxUsernameLength = 30,
        string CandidateReges = "@?[a-zA-Z0-9._]{1,30}",
        IReadOnlyCollection<string>? StopWords = null
    );
}

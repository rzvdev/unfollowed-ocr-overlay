using Unfollowed.Core.Models;

namespace Unfollowed.Core.Extraction;

public sealed class RegexUsernameExtractor : IUsernameExtractor
{
    public IReadOnlyList<MatchCandidate> ExtractCandidates(IReadOnlyCollection<(string Text, RectF RoiRect, float Confidence)> ocrTokens, ExtractionOptions options, Func<string, bool> isInNonFollowBackSet, Func<string, string> normalize)
    {
        throw new NotImplementedException();
    }
}

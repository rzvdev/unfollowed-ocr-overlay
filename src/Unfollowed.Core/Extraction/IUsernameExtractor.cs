using Unfollowed.Core.Models;

namespace Unfollowed.Core.Extraction;

public interface IUsernameExtractor
{
    IReadOnlyList<MatchCandidate> ExtractCandidates(IReadOnlyCollection<(string Text, RectF RoiRect, float Confidence)> ocrTokens,
        ExtractionOptions options,
        Func<string, bool> isInNonFollowBackSet,
        Func<string, string> normalize
    );
}

using Unfollowed.Core.Models;

namespace Unfollowed.Core.Extraction;

public interface IUsernameExtractor
{
    IReadOnlyList<MatchCandidate> ExtractCandiates(IReadOnlyCollection<(string Text, RectF RoiRect, float Confidence)> ocrTokens,
        ExtractionOptions options,
        Func<string, bool> isInNonFollowBackSet,
        Func<string, string> normalize
    );
}

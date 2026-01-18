using Unfollowed.Core.Models;

namespace Unfollowed.Core.Stabilization
{
    public interface IHighlightStabilizer
    {
        IReadOnlyList<Highlight> Stabilize(
            IReadOnlyList<MatchCandidate> candidates,
            RoiToScreenTransform transform,
            StabilizerOptions options);

        void Reset();
    }
}

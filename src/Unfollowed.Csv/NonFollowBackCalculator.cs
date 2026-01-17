using Unfollowed.Core.Models;

namespace Unfollowed.Csv;

public sealed class NonFollowBackCalculator : INonFollowBackCalculator
{
    public NonFollowBackData Compute(CsvImportResult following, CsvImportResult followers)
    {
        var followingSet = new HashSet<string>(following.UsernamesNormalized, StringComparer.Ordinal);
        var followersSet = new HashSet<string>(followers.UsernamesNormalized, StringComparer.Ordinal);

        var nonFollowBack = followingSet
            .Where(u => !followersSet.Contains(u))
            .OrderBy(u => u, StringComparer.Ordinal)
            .ToArray();

        return new NonFollowBackData(
            Following: followingSet.ToArray(),
            Followers: followersSet.ToArray(),
            NonFollowBack: nonFollowBack,
            FollowingStats: following.Stats,
            FollowersStats: followers.Stats
        );
    }
}

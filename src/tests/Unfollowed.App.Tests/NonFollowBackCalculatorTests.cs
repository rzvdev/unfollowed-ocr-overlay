using Unfollowed.Core.Models;
using Unfollowed.Csv;

namespace Unfollowed.App.Tests;

public sealed class NonFollowBackCalculatorTests
{
    [Fact]
    public void Compute_Returns_NonFollowBack_With_Ordinal_Sort_And_Sets()
    {
        var followingStats = new CsvImportStats(12, 8, 2, 2);
        var followersStats = new CsvImportStats(9, 7, 1, 1);

        var following = new CsvImportResult(
            new[] { "charlie", "alice", "bob", "bob", "delta", "alice" },
            followingStats,
            "username");
        var followers = new CsvImportResult(
            new[] { "bob", "erin", "bob" },
            followersStats,
            "username");

        var calculator = new NonFollowBackCalculator();

        var result = calculator.Compute(following, followers);

        Assert.Equal(
            new[] { "alice", "charlie", "delta" },
            result.NonFollowBack);

        Assert.Equal(
            new[] { "alice", "bob", "charlie", "delta" },
            result.Following.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "bob", "erin" },
            result.Followers.OrderBy(name => name, StringComparer.Ordinal));

        Assert.Same(followingStats, result.FollowingStats);
        Assert.Same(followersStats, result.FollowersStats);
    }
}

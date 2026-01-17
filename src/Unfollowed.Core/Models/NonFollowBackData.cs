namespace Unfollowed.Core.Models;

public sealed record NonFollowBackData(
    IReadOnlyCollection<string> Following,
    IReadOnlyCollection<string> Followers,
    IReadOnlyCollection<string> NonFollowBack,
    CsvImportStats FollowingStats,
    CsvImportStats FollowersStats
    );

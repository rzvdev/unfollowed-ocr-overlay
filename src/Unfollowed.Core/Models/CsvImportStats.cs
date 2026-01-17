namespace Unfollowed.Core.Models;

public sealed record CsvImportStats(
        int TotalRows,
        int ValidUsernames,
        int InvalidRows,
        int DuplicatesIgnored
    );

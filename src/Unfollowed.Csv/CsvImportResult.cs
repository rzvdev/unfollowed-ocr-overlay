using Unfollowed.Core.Models;

namespace Unfollowed.Csv;

public sealed record CsvImportResult(
    IReadOnlyCollection<string> UsernamesNormalized,
    CsvImportStats Stats,
    string? DetecteUsernameColumn
    );

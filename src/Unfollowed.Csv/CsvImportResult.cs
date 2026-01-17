using Unfollowed.Core.Models;

namespace Unfollowed.Csv;

public sealed record CsvImportResult(
    IReadOnlyCollection<string> UsernameNormalized,
    CsvImportStats Stats,
    string? DetecteUsernameColumn
    );

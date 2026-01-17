namespace Unfollowed.Csv;

public sealed record CsvImportOptions(
    string? UsernameColumnHint = null,
    bool HasHeader = true,
    char Delimiter = ',',
    int MaxRows = 200_000
);

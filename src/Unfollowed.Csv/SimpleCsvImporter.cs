
using Unfollowed.Core.Models;
using Unfollowed.Core.Normalization;

namespace Unfollowed.Csv;

public sealed class SimpleCsvImporter : ICsvImporter
{
    private readonly IUsernameNormalizer _normalizer;

    public SimpleCsvImporter(IUsernameNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public CsvImportResult ImportUsernames(string csvPath, CsvImportOptions options, CancellationToken ct)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException("CSV file not found.", csvPath);

        var lines = File.ReadLines(csvPath)
            .Take(options.MaxRows + (options.HasHeader ? 1 : 0))
            .ToList();

        if (lines.Count == 0)
            throw new InvalidOperationException("CSV file is empty.");

        int totalRows = 0;
        int invalid = 0;
        int duplicates = 0;

        string[]? header = null;
        int usernameIndex = 0;
        int start = 0;

        if (options.HasHeader)
        {
            header = Split(lines[0], options.Delimiter);
            usernameIndex = DetectUsernameColumnIndex(header, options.UsernameColumnHint);
            start = 1;
        }

        var set = new HashSet<string>(StringComparer.Ordinal);

        for (int i = start; i < lines.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            totalRows++;

            var parts = Split(lines[i], options.Delimiter);
            if (usernameIndex < 0 || usernameIndex >= parts.Length)
            {
                invalid++;
                continue;
            }

            var normalized = _normalizer.Normalize(parts[usernameIndex]);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                invalid++;
                continue;
            }

            if (!set.Add(normalized))
                duplicates++;
        }

        var stats = new CsvImportStats(
            TotalRows: totalRows,
            ValidUsernames: set.Count,
            InvalidRows: invalid,
            DuplicatesIgnored: duplicates
        );

        string? detected = null;
        if (header is not null && usernameIndex >= 0 && usernameIndex < header.Length)
            detected = header[usernameIndex];

        return new CsvImportResult(set.ToArray(), stats, detected);
    }

    private static int DetectUsernameColumnIndex(string[] header, string? hint)
    {
        if (!string.IsNullOrWhiteSpace(hint))
        {
            var idx = Array.FindIndex(header, h => string.Equals(h.Trim(), hint, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) return idx;
        }

        var candidates = new[] { "username", "user_name", "user name", "handle", "account" };
        foreach (var c in candidates)
        {
            var idx = Array.FindIndex(header, h => string.Equals(h.Trim(), c, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) return idx;
        }

        return 0;
    }

    private static string[] Split(string line, char delimiter) => line.Split(delimiter);
}

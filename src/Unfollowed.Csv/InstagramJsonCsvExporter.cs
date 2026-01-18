using System.Text.Json;

namespace Unfollowed.Csv;

public sealed class InstagramJsonCsvExporter
{
    public void Export(string followingJsonPath, string followersJsonPath, string outputDirectory, CancellationToken ct)
    {
        if (!File.Exists(followingJsonPath))
            throw new FileNotFoundException("Following JSON file not found.", followingJsonPath);

        if (!File.Exists(followersJsonPath))
            throw new FileNotFoundException("Followers JSON file not found.", followersJsonPath);

        Directory.CreateDirectory(outputDirectory);

        var followingUsernames = ReadFollowingUsernames(followingJsonPath, ct);
        var followersUsernames = ReadFollowersUsernames(followersJsonPath, ct);

        var followingCsvPath = Path.Combine(outputDirectory, "following.csv");
        var followersCsvPath = Path.Combine(outputDirectory, "followers.csv");

        WriteCsv(followingCsvPath, followingUsernames, ct);
        WriteCsv(followersCsvPath, followersUsernames, ct);
    }

    private static IReadOnlyList<string> ReadFollowingUsernames(string path, CancellationToken ct)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("relationships_following", out var followingArray)
            || followingArray.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("following.json is missing relationships_following array.");
        }

        return ReadUsernamesFromArray(followingArray, ct);
    }

    private static IReadOnlyList<string> ReadFollowersUsernames(string path, CancellationToken ct)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("followers.json must be a JSON array.");

        return ReadUsernamesFromArray(document.RootElement, ct);
    }

    private static IReadOnlyList<string> ReadUsernamesFromArray(JsonElement array, CancellationToken ct)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in array.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            var username = ExtractUsername(entry);
            if (string.IsNullOrWhiteSpace(username))
                continue;

            if (seen.Add(username))
                results.Add(username);
        }

        return results;
    }

    private static string? ExtractUsername(JsonElement entry)
    {
        if (entry.TryGetProperty("title", out var titleElement))
        {
            var title = titleElement.GetString();
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }

        if (entry.TryGetProperty("string_list_data", out var listElement)
            && listElement.ValueKind == JsonValueKind.Array
            && listElement.GetArrayLength() > 0)
        {
            var first = listElement[0];
            if (first.TryGetProperty("value", out var valueElement))
            {
                var value = valueElement.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            if (first.TryGetProperty("href", out var hrefElement))
            {
                var href = hrefElement.GetString();
                if (!string.IsNullOrWhiteSpace(href))
                    return ExtractUsernameFromHref(href);
            }
        }

        return null;
    }

    private static string? ExtractUsernameFromHref(string href)
    {
        if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
            return null;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;

        return segments[^1];
    }

    private static void WriteCsv(string path, IReadOnlyList<string> usernames, CancellationToken ct)
    {
        using var writer = new StreamWriter(path, false);
        writer.WriteLine("username");

        foreach (var username in usernames)
        {
            ct.ThrowIfCancellationRequested();
            writer.WriteLine(username);
        }
    }
}

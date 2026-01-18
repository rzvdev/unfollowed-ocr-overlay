using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unfollowed.Core.Models;

namespace Unfollowed.Core.Extraction;

/// <summary>
/// Extracts username candidates by applying a configurable regex to OCR tokens.
/// </summary>
public sealed class RegexUsernameExtractor : IUsernameExtractor
{
    /// <summary>
    /// Extracts candidate usernames from OCR tokens, applying filtering and normalization.
    /// </summary>
    public IReadOnlyList<MatchCandidate> ExtractCandidates(IReadOnlyCollection<(string Text, RectF RoiRect, float Confidence)> ocrTokens, ExtractionOptions options, Func<string, bool> isInNonFollowBackSet, Func<string, string> normalize)
    {
        if (ocrTokens.Count == 0)
            return Array.Empty<MatchCandidate>();

        var stopWords = BuildStopWords(options.StopWords);
        var regex = new Regex(options.CandidateReges, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bestByUser = new Dictionary<string, MatchCandidate>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in ocrTokens)
        {
            // Skip tokens that are too weak or empty to avoid noisy candidates.
            if (token.Confidence < options.MinTokenConfidence)
                continue;

            if (string.IsNullOrWhiteSpace(token.Text))
                continue;

            foreach (Match match in regex.Matches(token.Text))
            {
                if (!match.Success)
                    continue;

                // Normalize before stop word filtering and additional validation.
                var normalized = normalize(match.Value);
                if (string.IsNullOrWhiteSpace(normalized))
                    continue;

                if (stopWords.Contains(normalized))
                    continue;

                if (!IsInstagramHandle(normalized, options.MaxUsernameLength))
                    continue;

                if (!isInNonFollowBackSet(normalized))
                    continue;

                var candidate = new MatchCandidate(normalized, token.Text, token.Confidence, token.RoiRect);
                if (bestByUser.TryGetValue(normalized, out var existing))
                {
                    // Keep the highest confidence hit per username for the frame.
                    if (candidate.Confidence > existing.Confidence)
                        bestByUser[normalized] = candidate;
                    continue;
                }

                bestByUser[normalized] = candidate;
            }
        }

        return bestByUser.Values
            .OrderByDescending(candidate => candidate.Confidence)
            .ToArray();
    }

    private static HashSet<string> BuildStopWords(IReadOnlyCollection<string>? stopWords)
    {
        var set = stopWords is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(stopWords, StringComparer.OrdinalIgnoreCase);

        set.Add("following");
        set.Add("message");
        set.Add("follow");
        set.Add("followers");
        set.Add("requested");
        set.Add("request");
        set.Add("posts");
        set.Add("post");
        set.Add("block");
        set.Add("blocked");
        set.Add("remove");
        set.Add("share");
        set.Add("like");

        return set;
    }

    /// <summary>
    /// Validates the normalized username using Instagram-style rules.
    /// </summary>
    private static bool IsInstagramHandle(string normalized, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        if (normalized.Length > maxLength)
            return false;

        if (normalized.StartsWith('.') || normalized.EndsWith('.'))
            return false;

        for (var i = 0; i < normalized.Length; i++)
        {
            var ch = normalized[i];
            if (ch == '.' && i > 0 && normalized[i - 1] == '.')
                return false;
        }

        return true;
    }
}

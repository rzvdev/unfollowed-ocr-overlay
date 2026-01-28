using Unfollowed.Core.Models;

namespace Unfollowed.Core.Stabilization;

/// <summary>
/// Stabilizes highlights by requiring a username to appear in K of the last M frames.
/// </summary>
public sealed class KOfMHighlightStabilizer : IHighlightStabilizer
{
    private readonly Queue<Dictionary<string, MatchCandidate>> _frames = new();

    /// <summary>
    /// Converts recent OCR candidates into stable highlights based on a sliding window.
    /// </summary>
    public IReadOnlyList<Highlight> Stabilize(
        IReadOnlyList<MatchCandidate> candidates,
        RoiToScreenTransform transform,
        StabilizerOptions options)
    {
        if (options.WindowSizeM <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "WindowSizeM must be greater than zero.");
        }

        if (options.RequiredK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "RequiredK must be greater than zero.");
        }

        // Reduce each frame to the best candidate per username.
        var frameCandidates = candidates
            .Where(candidate => candidate.Confidence >= options.ConfidenceThreshold)
            .GroupBy(candidate => candidate.UsernameNormalized, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.Confidence).First())
            .ToDictionary(candidate => candidate.UsernameNormalized, StringComparer.OrdinalIgnoreCase);

        _frames.Enqueue(frameCandidates);
        while (_frames.Count > options.WindowSizeM)
        {
            _frames.Dequeue();
        }

        // Collect all usernames seen in the active window.
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frame in _frames)
        {
            foreach (var username in frame.Keys)
            {
                usernames.Add(username);
            }
        }

        var highlights = new List<Highlight>();
        foreach (var username in usernames)
        {
            var occurrences = _frames.Count(frame => frame.ContainsKey(username));
            if (occurrences >= options.RequiredK)
            {
                var candidate = FindMostRecentCandidate(username, frameCandidates);
                if (candidate is null)
                {
                    continue;
                }

                highlights.Add(new Highlight(
                    candidate.UsernameNormalized,
                    candidate.OcrText,
                    candidate.Confidence,
                    transform.ToScreen(candidate.RoiRect),
                    true));
            }
            else if (options.AllowUncertainHighlights && frameCandidates.TryGetValue(username, out var candidate))
            {
                highlights.Add(new Highlight(
                    candidate.UsernameNormalized,
                    candidate.OcrText,
                    candidate.Confidence,
                    transform.ToScreen(candidate.RoiRect),
                    false));
            }
        }

        return highlights;
    }

    public void Reset()
    {
        _frames.Clear();
    }

    /// <summary>
    /// Finds the most recent candidate for a username within the active window.
    /// </summary>
    private MatchCandidate? FindMostRecentCandidate(
        string username,
        IReadOnlyDictionary<string, MatchCandidate> mostRecentFrame)
    {
        return mostRecentFrame.TryGetValue(username, out var candidate) ? candidate : null;
    }
}

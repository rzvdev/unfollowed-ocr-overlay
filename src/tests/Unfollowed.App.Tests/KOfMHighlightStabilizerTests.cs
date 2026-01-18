using Unfollowed.Core.Models;
using Unfollowed.Core.Stabilization;

namespace Unfollowed.App.Tests;

public sealed class KOfMHighlightStabilizerTests
{
    [Fact]
    public void Stabilize_Returns_Stable_Highlight_When_Seen_In_K_Of_M_Frames()
    {
        var stabilizer = new KOfMHighlightStabilizer();
        var options = new StabilizerOptions(WindowSizeM: 3, RequiredK: 2, ConfidenceThreshold: 0.5f);
        var transform = new RoiToScreenTransform(100, 50, 200, 100, 400, 200);

        stabilizer.Stabilize(
            new[] { new MatchCandidate("alice", 0.9f, new RectF(10, 10, 20, 10)) },
            transform,
            options);

        stabilizer.Stabilize(
            new[] { new MatchCandidate("bob", 0.9f, new RectF(40, 20, 30, 10)) },
            transform,
            options);

        var highlights = stabilizer.Stabilize(
            new[] { new MatchCandidate("alice", 0.95f, new RectF(12, 12, 20, 10)) },
            transform,
            options);

        var highlight = Assert.Single(highlights);
        Assert.Equal("alice", highlight.UsernameNormalized);
        Assert.True(highlight.IsCertain);
        Assert.Equal(new RectF(106, 56, 10, 5), highlight.ScreenRect);
    }

    [Fact]
    public void Stabilize_Does_Not_Return_Highlight_When_Seen_In_One_Frame()
    {
        var stabilizer = new KOfMHighlightStabilizer();
        var options = new StabilizerOptions(WindowSizeM: 3, RequiredK: 2, ConfidenceThreshold: 0.5f);
        var transform = new RoiToScreenTransform(100, 50, 200, 100, 400, 200);

        stabilizer.Stabilize(
            new[] { new MatchCandidate("bob", 0.9f, new RectF(40, 20, 30, 10)) },
            transform,
            options);

        stabilizer.Stabilize(Array.Empty<MatchCandidate>(), transform, options);
        var highlights = stabilizer.Stabilize(Array.Empty<MatchCandidate>(), transform, options);

        Assert.Empty(highlights);
    }
}

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
            new[] { new MatchCandidate("alice", "@alice", 0.9f, new RectF(10, 10, 20, 10)) },
            transform,
            options);

        stabilizer.Stabilize(
            new[] { new MatchCandidate("bob", "@bob", 0.9f, new RectF(40, 20, 30, 10)) },
            transform,
            options);

        var highlights = stabilizer.Stabilize(
            new[] { new MatchCandidate("alice", "@alice", 0.95f, new RectF(12, 12, 20, 10)) },
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
            new[] { new MatchCandidate("bob", "@bob", 0.9f, new RectF(40, 20, 30, 10)) },
            transform,
            options);

        stabilizer.Stabilize(Array.Empty<MatchCandidate>(), transform, options);
        var highlights = stabilizer.Stabilize(Array.Empty<MatchCandidate>(), transform, options);

        Assert.Empty(highlights);
    }

    [Fact]
    public void Stabilize_Returns_Uncertain_Highlight_When_Allowed()
    {
        var stabilizer = new KOfMHighlightStabilizer();
        var options = new StabilizerOptions(WindowSizeM: 3, RequiredK: 2, ConfidenceThreshold: 0.5f, AllowUncertainHighlights: true);
        var transform = new RoiToScreenTransform(100, 50, 200, 100, 400, 200);

        var highlights = stabilizer.Stabilize(
            new[] { new MatchCandidate("cora", "@cora", 0.8f, new RectF(20, 10, 20, 10)) },
            transform,
            options);

        var highlight = Assert.Single(highlights);
        Assert.Equal("cora", highlight.UsernameNormalized);
        Assert.False(highlight.IsCertain);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 0)]
    public void Stabilize_Throws_When_WindowOrKInvalid(int windowSize, int requiredK)
    {
        var stabilizer = new KOfMHighlightStabilizer();
        var options = new StabilizerOptions(WindowSizeM: windowSize, RequiredK: requiredK, ConfidenceThreshold: 0.5f);
        var transform = new RoiToScreenTransform(100, 50, 200, 100, 400, 200);

        Assert.Throws<ArgumentOutOfRangeException>(() => stabilizer.Stabilize(
            Array.Empty<MatchCandidate>(),
            transform,
            options));
    }

    [Fact]
    public void Reset_ClearsTrackedFrames()
    {
        var stabilizer = new KOfMHighlightStabilizer();
        var options = new StabilizerOptions(WindowSizeM: 2, RequiredK: 2, ConfidenceThreshold: 0.5f);
        var transform = new RoiToScreenTransform(0, 0, 100, 100, 100, 100);

        stabilizer.Stabilize(
            new[] { new MatchCandidate("dana", "@dana", 0.8f, new RectF(10, 10, 10, 10)) },
            transform,
            options);

        stabilizer.Reset();

        var highlights = stabilizer.Stabilize(
            new[] { new MatchCandidate("dana", "@dana", 0.8f, new RectF(10, 10, 10, 10)) },
            transform,
            options);

        Assert.Empty(highlights);
    }
}

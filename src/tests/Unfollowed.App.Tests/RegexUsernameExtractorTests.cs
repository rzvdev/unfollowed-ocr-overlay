using System.Collections.Generic;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Models;
using Unfollowed.Core.Normalization;

namespace Unfollowed.App.Tests;

public sealed class RegexUsernameExtractorTests
{
    private static readonly RectF SampleRect = new(10, 20, 30, 40);

    [Fact]
    public void ExtractCandidates_AllowsHandlesWithPunctuation()
    {
        var extractor = new RegexUsernameExtractor();
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var tokens = new[]
        {
            ("hello @User.Name,", SampleRect, 0.93f),
        };
        var nonFollowBackUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "user.name",
        };

        var candidates = extractor.ExtractCandidates(
            tokens,
            new ExtractionOptions(),
            nonFollowBackUsers.Contains,
            normalizer.Normalize);

        var candidate = Assert.Single(candidates);
        Assert.Equal("user.name", candidate.UsernameNormalized);
        Assert.Equal(0.93f, candidate.Confidence);
        Assert.Equal(SampleRect, candidate.RoiRect);
    }

    [Fact]
    public void ExtractCandidates_StripsLeadingAt()
    {
        var extractor = new RegexUsernameExtractor();
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var tokens = new[]
        {
            ("@Example", SampleRect, 0.8f),
        };

        var candidates = extractor.ExtractCandidates(
            tokens,
            new ExtractionOptions(),
            _ => true,
            normalizer.Normalize);

        var candidate = Assert.Single(candidates);
        Assert.Equal("example", candidate.UsernameNormalized);
    }

    [Fact]
    public void ExtractCandidates_FiltersFalsePositives()
    {
        var extractor = new RegexUsernameExtractor();
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var tokens = new[]
        {
            ("Following", SampleRect, 0.9f),
            ("Message", SampleRect, 0.9f),
        };

        var candidates = extractor.ExtractCandidates(
            tokens,
            new ExtractionOptions(),
            _ => true,
            normalizer.Normalize);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ExtractCandidates_RespectsMinimumConfidence()
    {
        var extractor = new RegexUsernameExtractor();
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var tokens = new[]
        {
            ("@lowconf", SampleRect, 0.5f),
        };

        var candidates = extractor.ExtractCandidates(
            tokens,
            new ExtractionOptions(MinTokenConfidence: 0.8f),
            _ => true,
            normalizer.Normalize);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ExtractCandidates_FiltersHandlesWithConsecutiveDots()
    {
        var extractor = new RegexUsernameExtractor();
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var tokens = new[]
        {
            ("@user..name", SampleRect, 0.9f),
        };

        var candidates = extractor.ExtractCandidates(
            tokens,
            new ExtractionOptions(),
            _ => true,
            normalizer.Normalize);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ExtractCandidates_RespectsMaxUsernameLength()
    {
        var extractor = new RegexUsernameExtractor();
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var tokens = new[]
        {
            ("@thisiswaytoolongforourlimits", SampleRect, 0.9f),
        };

        var candidates = extractor.ExtractCandidates(
            tokens,
            new ExtractionOptions(MaxUsernameLength: 10),
            _ => true,
            normalizer.Normalize);

        Assert.Empty(candidates);
    }
}

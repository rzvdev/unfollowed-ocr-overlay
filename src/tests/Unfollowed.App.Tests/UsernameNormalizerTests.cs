using Unfollowed.Core.Normalization;

namespace Unfollowed.App.Tests;

public sealed class UsernameNormalizerTests
{
    [Fact]
    public void Normalize_StripsLeadingAtAndLowercases()
    {
        var options = new UsernameNormalizationOptions();
        var normalizer = new UsernameNormalizer(options);

        var normalized = normalizer.Normalize("@Example.User");

        Assert.Equal("example.user", normalized);
    }

    [Fact]
    public void Normalize_RemovesDisallowedCharacters()
    {
        var options = new UsernameNormalizationOptions();
        var normalizer = new UsernameNormalizer(options);

        var normalized = normalizer.Normalize("User! Name#");

        Assert.Equal("username", normalized);
    }

    [Fact]
    public void Normalize_EnforcesMinAndMaxLength()
    {
        var options = new UsernameNormalizationOptions
        {
            MinLength = 3,
            MaxLength = 5,
        };
        var normalizer = new UsernameNormalizer(options);

        Assert.Equal(string.Empty, normalizer.Normalize("ab"));
        Assert.Equal("abcde", normalizer.Normalize("abcdefg"));
    }
}

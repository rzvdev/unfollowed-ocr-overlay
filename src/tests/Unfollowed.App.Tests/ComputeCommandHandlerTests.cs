using Unfollowed.App.Commands;
using Unfollowed.Core.Normalization;
using Unfollowed.Csv;

namespace Unfollowed.App.Tests;

public sealed class ComputeCommandHandlerTests
{
    [Fact]
    public void Execute_Computes_NonFollowBack_Correctly()
    {
        // Arrange
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var importer = new SimpleCsvImporter(normalizer);
        var calc = new NonFollowBackCalculator();
        var handler = new ComputeCommandHandler(importer, calc);

        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "following.csv");
        var followersPath = Path.Combine(dir, "followers.csv");

        File.WriteAllText(followingPath, "username\nalice\nbob\ncharlie\n@Dan_01\n");
        File.WriteAllText(followersPath, "username\nbob\ndan_01\n");

        // Act
        var result = handler.Execute(followingPath, followersPath, CancellationToken.None);

        // Assert
        Assert.Equal(4, result.Following);
        Assert.Equal(2, result.Followers);
        Assert.Equal(2, result.NonFollowBack);
    }
}
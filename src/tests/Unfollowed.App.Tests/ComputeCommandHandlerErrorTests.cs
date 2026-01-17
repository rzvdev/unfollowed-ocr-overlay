using Unfollowed.App.Commands;
using Unfollowed.Core.Normalization;
using Unfollowed.Csv;

namespace Unfollowed.App.Tests;

public sealed class ComputeCommandHandlerErrorTests
{
    [Fact]
    public void Execute_Throws_When_FileMissing()
    {
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var importer = new SimpleCsvImporter(normalizer);
        var calc = new NonFollowBackCalculator();
        var handler = new ComputeCommandHandler(importer, calc);

        Assert.Throws<FileNotFoundException>(() =>
            handler.Execute("missing_following.csv", "missing_followers.csv", CancellationToken.None));
    }

    [Fact]
    public void ImportUsernames_Throws_When_EmptyFile()
    {
        var normalizer = new UsernameNormalizer(new UsernameNormalizationOptions());
        var importer = new SimpleCsvImporter(normalizer);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        File.WriteAllText(path, "");

        Assert.Throws<InvalidOperationException>(() =>
            importer.ImportUsernames(path, new CsvImportOptions(), CancellationToken.None));
    }
}

using Unfollowed.Core.Normalization;
using Unfollowed.Csv;

namespace Unfollowed.App.Tests;

public sealed class SimpleCsvImporterTests
{
    [Fact]
    public void ImportUsernames_UsesHintForHeaderDetection()
    {
        var csv = string.Join(Environment.NewLine,
            "email,Handle,other",
            "a@example.com,@Foo,1",
            "b@example.com,@Bar,2");
        var path = WriteTempCsv(csv);

        try
        {
            var importer = CreateImporter();
            var options = new CsvImportOptions(UsernameColumnHint: "Handle");

            var result = importer.ImportUsernames(path, options, CancellationToken.None);

            Assert.Equal("Handle", result.DetectedUsernameColumn);
            Assert.Equal(new[] { "foo", "bar" }, result.UsernamesNormalized.OrderBy(x => x));
            Assert.Equal(2, result.Stats.TotalRows);
            Assert.Equal(2, result.Stats.ValidUsernames);
            Assert.Equal(0, result.Stats.InvalidRows);
            Assert.Equal(0, result.Stats.DuplicatesIgnored);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportUsernames_FallsBackToCandidateHeaderNames()
    {
        var csv = string.Join(Environment.NewLine,
            "email,USERNAME,other",
            "a@example.com,@One,1",
            "b@example.com,@Two,2");
        var path = WriteTempCsv(csv);

        try
        {
            var importer = CreateImporter();
            var options = new CsvImportOptions();

            var result = importer.ImportUsernames(path, options, CancellationToken.None);

            Assert.Equal("USERNAME", result.DetectedUsernameColumn);
            Assert.Equal(new[] { "one", "two" }, result.UsernamesNormalized.OrderBy(x => x));
            Assert.Equal(2, result.Stats.TotalRows);
            Assert.Equal(2, result.Stats.ValidUsernames);
            Assert.Equal(0, result.Stats.InvalidRows);
            Assert.Equal(0, result.Stats.DuplicatesIgnored);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportUsernames_WithoutHeader_UsesFirstColumn()
    {
        var csv = string.Join(Environment.NewLine,
            "@First,ignored",
            "Second,ignored");
        var path = WriteTempCsv(csv);

        try
        {
            var importer = CreateImporter();
            var options = new CsvImportOptions(HasHeader: false);

            var result = importer.ImportUsernames(path, options, CancellationToken.None);

            Assert.Null(result.DetectedUsernameColumn);
            Assert.Equal(new[] { "first", "second" }, result.UsernamesNormalized.OrderBy(x => x));
            Assert.Equal(2, result.Stats.TotalRows);
            Assert.Equal(2, result.Stats.ValidUsernames);
            Assert.Equal(0, result.Stats.InvalidRows);
            Assert.Equal(0, result.Stats.DuplicatesIgnored);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportUsernames_RespectsMaxRows()
    {
        var csv = string.Join(Environment.NewLine,
            "username,other",
            "first,1",
            "second,2",
            "third,3");
        var path = WriteTempCsv(csv);

        try
        {
            var importer = CreateImporter();
            var options = new CsvImportOptions(MaxRows: 2);

            var result = importer.ImportUsernames(path, options, CancellationToken.None);

            Assert.Equal(new[] { "first", "second" }, result.UsernamesNormalized.OrderBy(x => x));
            Assert.Equal(2, result.Stats.TotalRows);
            Assert.Equal(2, result.Stats.ValidUsernames);
            Assert.Equal(0, result.Stats.InvalidRows);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportUsernames_MarksInvalidRowsForMissingColumnsWhitespaceAndDelimiterEdges()
    {
        var csv = string.Join(Environment.NewLine,
            "id,name,username",
            "1,Jane",
            "2,John,   ",
            "3,Jamie,",
            "4,Sam,@Valid");
        var path = WriteTempCsv(csv);

        try
        {
            var importer = CreateImporter();
            var options = new CsvImportOptions();

            var result = importer.ImportUsernames(path, options, CancellationToken.None);

            Assert.Equal("username", result.DetectedUsernameColumn);
            Assert.Equal(new[] { "valid" }, result.UsernamesNormalized);
            Assert.Equal(4, result.Stats.TotalRows);
            Assert.Equal(1, result.Stats.ValidUsernames);
            Assert.Equal(3, result.Stats.InvalidRows);
            Assert.Equal(0, result.Stats.DuplicatesIgnored);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImportUsernames_CountsDuplicatesAfterNormalization()
    {
        var csv = string.Join(Environment.NewLine,
            "username",
            "@Foo",
            "foo",
            "FOO ",
            "bar");
        var path = WriteTempCsv(csv);

        try
        {
            var importer = CreateImporter();
            var options = new CsvImportOptions();

            var result = importer.ImportUsernames(path, options, CancellationToken.None);

            Assert.Equal("username", result.DetectedUsernameColumn);
            Assert.Equal(new[] { "bar", "foo" }, result.UsernamesNormalized.OrderBy(x => x));
            Assert.Equal(4, result.Stats.TotalRows);
            Assert.Equal(2, result.Stats.ValidUsernames);
            Assert.Equal(0, result.Stats.InvalidRows);
            Assert.Equal(2, result.Stats.DuplicatesIgnored);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SimpleCsvImporter CreateImporter()
    {
        var options = new UsernameNormalizationOptions();
        var normalizer = new UsernameNormalizer(options);
        return new SimpleCsvImporter(normalizer);
    }

    private static string WriteTempCsv(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents);
        return path;
    }
}

using Unfollowed.Csv;

namespace Unfollowed.App.Tests;

public sealed class InstagramJsonCsvExporterTests
{
    [Fact]
    public void Export_CreatesCsvFilesFromJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "following.json");
        var followersPath = Path.Combine(dir, "followers.json");
        var outputDir = Path.Combine(dir, "out");

        File.WriteAllText(followingPath, """
        {
          "relationships_following": [
            {
              "title": "alice",
              "string_list_data": [
                { "href": "https://www.instagram.com/_u/alice", "timestamp": 1 }
              ]
            },
            {
              "title": "",
              "string_list_data": [
                { "href": "https://www.instagram.com/_u/bob", "value": "bob", "timestamp": 2 }
              ]
            }
          ]
        }
        """);

        File.WriteAllText(followersPath, """
        [
          {
            "title": "",
            "media_list_data": [],
            "string_list_data": [
              { "href": "https://www.instagram.com/charlie", "value": "charlie", "timestamp": 3 }
            ]
          }
        ]
        """);

        var exporter = new InstagramJsonCsvExporter();
        exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None);

        var followingCsv = File.ReadAllLines(Path.Combine(outputDir, "following.csv"));
        var followersCsv = File.ReadAllLines(Path.Combine(outputDir, "followers.csv"));

        Assert.Equal(new[] { "username", "alice", "bob" }, followingCsv);
        Assert.Equal(new[] { "username", "charlie" }, followersCsv);
    }

    [Fact]
    public void Export_UsesTitleAndStringListDataForFollowing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "following.json");
        var followersPath = Path.Combine(dir, "followers.json");
        var outputDir = Path.Combine(dir, "out");

        File.WriteAllText(followingPath, """
        {
          "relationships_following": [
            {
              "title": "diana",
              "string_list_data": [
                { "href": "https://www.instagram.com/_u/diana", "timestamp": 1 }
              ]
            },
            {
              "title": "",
              "string_list_data": [
                { "href": "https://www.instagram.com/_u/erin", "value": "erin", "timestamp": 2 }
              ]
            }
          ]
        }
        """);

        File.WriteAllText(followersPath, "[]");

        var exporter = new InstagramJsonCsvExporter();
        exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None);

        var followingCsv = File.ReadAllLines(Path.Combine(outputDir, "following.csv"));

        Assert.Equal(new[] { "username", "diana", "erin" }, followingCsv);
    }

    [Fact]
    public void Export_ParsesFollowersArrayFromValueAndHref()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "following.json");
        var followersPath = Path.Combine(dir, "followers.json");
        var outputDir = Path.Combine(dir, "out");

        File.WriteAllText(followingPath, """
        {
          "relationships_following": []
        }
        """);

        File.WriteAllText(followersPath, """
        [
          {
            "title": "",
            "string_list_data": [
              { "value": "frank", "timestamp": 3 }
            ]
          },
          {
            "title": "",
            "string_list_data": [
              { "href": "https://www.instagram.com/grace", "timestamp": 4 }
            ]
          }
        ]
        """);

        var exporter = new InstagramJsonCsvExporter();
        exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None);

        var followersCsv = File.ReadAllLines(Path.Combine(outputDir, "followers.csv"));

        Assert.Equal(new[] { "username", "frank", "grace" }, followersCsv);
    }

    [Fact]
    public void Export_DeduplicatesUsernamesAcrossEntries()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "following.json");
        var followersPath = Path.Combine(dir, "followers.json");
        var outputDir = Path.Combine(dir, "out");

        File.WriteAllText(followingPath, """
        {
          "relationships_following": [
            {
              "title": "helen",
              "string_list_data": [
                { "href": "https://www.instagram.com/helen", "timestamp": 1 }
              ]
            },
            {
              "title": "",
              "string_list_data": [
                { "value": "helen", "timestamp": 2 }
              ]
            }
          ]
        }
        """);

        File.WriteAllText(followersPath, """
        [
          {
            "title": "",
            "string_list_data": [
              { "href": "https://www.instagram.com/helen", "timestamp": 3 }
            ]
          },
          {
            "title": "",
            "string_list_data": [
              { "value": "helen", "timestamp": 4 }
            ]
          }
        ]
        """);

        var exporter = new InstagramJsonCsvExporter();
        exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None);

        var followingCsv = File.ReadAllLines(Path.Combine(outputDir, "following.csv"));
        var followersCsv = File.ReadAllLines(Path.Combine(outputDir, "followers.csv"));

        Assert.Equal(new[] { "username", "helen" }, followingCsv);
        Assert.Equal(new[] { "username", "helen" }, followersCsv);
    }

    [Fact]
    public void Export_ThrowsWhenFollowingJsonMissingRelationshipsArray()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "following.json");
        var followersPath = Path.Combine(dir, "followers.json");
        var outputDir = Path.Combine(dir, "out");

        File.WriteAllText(followingPath, """
        {
          "relationships_followed_by": []
        }
        """);

        File.WriteAllText(followersPath, "[]");

        var exporter = new InstagramJsonCsvExporter();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None));

        Assert.Equal("following.json is missing relationships_following array.", ex.Message);
    }

    [Fact]
    public void Export_ThrowsWhenFollowersJsonIsNotArray()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "following.json");
        var followersPath = Path.Combine(dir, "followers.json");
        var outputDir = Path.Combine(dir, "out");

        File.WriteAllText(followingPath, """
        {
          "relationships_following": []
        }
        """);

        File.WriteAllText(followersPath, """
        {
          "not": "array"
        }
        """);

        var exporter = new InstagramJsonCsvExporter();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None));

        Assert.Equal("followers.json must be a JSON array.", ex.Message);
    }

    [Fact]
    public void Export_ThrowsWhenFilesMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gui-unfollowed-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        var followingPath = Path.Combine(dir, "missing-following.json");
        var followersPath = Path.Combine(dir, "missing-followers.json");
        var outputDir = Path.Combine(dir, "out");

        var exporter = new InstagramJsonCsvExporter();

        var followingEx = Assert.Throws<FileNotFoundException>(() =>
            exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None));

        Assert.Equal(followingPath, followingEx.FileName);

        File.WriteAllText(followingPath, """
        {
          "relationships_following": []
        }
        """);

        var followersEx = Assert.Throws<FileNotFoundException>(() =>
            exporter.Export(followingPath, followersPath, outputDir, CancellationToken.None));

        Assert.Equal(followersPath, followersEx.FileName);
    }
}

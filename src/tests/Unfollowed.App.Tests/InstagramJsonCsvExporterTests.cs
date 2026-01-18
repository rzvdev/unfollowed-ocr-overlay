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
}

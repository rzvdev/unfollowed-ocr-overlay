using Unfollowed.Csv;

namespace Unfollowed.App.Commands;

public sealed class ComputeCommandHandler
{
    private readonly ICsvImporter _importer;
    private readonly INonFollowBackCalculator _calculator;

    public ComputeCommandHandler(ICsvImporter importer, INonFollowBackCalculator calculator)
    {
        _importer = importer;
        _calculator = calculator;
    }

    public (int Following, int Followers, int NonFollowBack) Execute(string followingPath, string followersPath, CancellationToken ct)
    {
        var following = _importer.ImportUsernames(followingPath, new CsvImportOptions(), ct);
        var followers = _importer.ImportUsernames(followersPath, new CsvImportOptions(), ct);

        var data = _calculator.Compute(following, followers);

        return (data.Following.Count, data.Followers.Count, data.NonFollowBack.Count);
    }
}

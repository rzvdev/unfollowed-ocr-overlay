
namespace Unfollowed.Csv;

public sealed class SimpleCsvImporter : ICsvImporter
{
    public SimpleCsvImporter()
    {
    }

    public CsvImportResult ImportUsernames(string csvPath, CsvImportOptions options, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

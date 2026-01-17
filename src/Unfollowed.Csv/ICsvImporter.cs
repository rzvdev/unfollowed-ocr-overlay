namespace Unfollowed.Csv;

public interface ICsvImporter
{
    CsvImportResult ImportUsernames(string csvPath, CsvImportOptions options, CancellationToken ct);
}

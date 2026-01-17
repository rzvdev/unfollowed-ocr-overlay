using Unfollowed.Core.Models;

namespace Unfollowed.Csv;

public sealed class NonFollowBackCalculator : INonFollowBackCalculator
{
    public NonFollowBackData Compute(CsvImportResult following, CsvImportResult followers)
    {
        throw new NotImplementedException();
    }
}

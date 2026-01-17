using Unfollowed.Core.Models;

namespace Unfollowed.Csv;

public interface INonFollowBackCalculator
{
    NonFollowBackData Compute(CsvImportResult following, CsvImportResult followers);
}

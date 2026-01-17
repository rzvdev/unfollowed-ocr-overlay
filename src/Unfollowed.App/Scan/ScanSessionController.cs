using Unfollowed.Capture;
using Unfollowed.Core.Models;

namespace Unfollowed.App.Scan;

public sealed class ScanSessionController : IScanSessionController
{
    public Task StartAsync(NonFollowBackData data, RoiSelection roi, ScanSessionOptions options, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

using Unfollowed.Capture;
using Unfollowed.Core.Models;

namespace Unfollowed.App.Scan;

public interface IScanSessionController
{
    Task? SessionTask { get; }
    Task StartAsync(NonFollowBackData data, RoiSelection roi, ScanSessionOptions options, CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}

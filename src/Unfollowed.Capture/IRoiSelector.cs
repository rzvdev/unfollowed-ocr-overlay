namespace Unfollowed.Capture;

public interface IRoiSelector
{
    Task<RoiSelection> SelectRegionAsync(CancellationToken ct);
}


namespace Unfollowed.Capture;

public sealed class StubRoiSelector : IRoiSelector
{
    public Task<RoiSelection> SelectRegionAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

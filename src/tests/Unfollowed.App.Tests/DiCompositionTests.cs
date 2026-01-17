using Microsoft.Extensions.DependencyInjection;
using Unfollowed.App.Scan;
using Unfollowed.Csv;

namespace Unfollowed.App.Tests;

public sealed class DiCompositionTests
{
    [Fact]
    public void Container_CanResolve_KeyServices()
    {
        var sp = AppHost.BuildServiceProvider();

        _ = sp.GetRequiredService<ICsvImporter>();
        _ = sp.GetRequiredService<INonFollowBackCalculator>();
        _ = sp.GetRequiredService<IScanSessionController>();
    }
}

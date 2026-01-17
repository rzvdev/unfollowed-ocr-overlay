using Microsoft.Extensions.DependencyInjection;
using Unfollowed.App.Composition;

namespace Unfollowed.App
{
    public sealed class AppHost
    {
        public static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            services.AddUnfollowedCore()
                    .AddUnfollowedCsv()
                    .AddUnfollowedApp()
                    .AddUnfollowedRuntimeStubs();

            return services.BuildServiceProvider();
        }
    }
}

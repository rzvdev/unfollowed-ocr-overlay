using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unfollowed.App.Composition;

namespace Unfollowed.App
{
    public sealed class AppHost
    {
        public static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();
            services.AddUnfollowedCore()
                    .AddUnfollowedCsv()
                    .AddUnfollowedApp()
                    .AddUnfollowedRuntimeStubs(configuration);
            services.AddSingleton<Unfollowed.Preprocess.IFramePreprocessor, Unfollowed.Preprocess.NoOpFramePreprocessor>();

            return services.BuildServiceProvider();
        }
    }
}

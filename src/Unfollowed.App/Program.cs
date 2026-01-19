using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unfollowed.App.CliCore;
using Unfollowed.App.Composition;

namespace Unfollowed.App;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(b =>
        {
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            b.SetMinimumLevel(LogLevel.Information);
        });

        services
            .AddUnfollowedCore()
            .AddUnfollowedCsv()
            .AddUnfollowedApp()
            .AddUnfollowedRuntimeStubs(configuration);

        var provider = services.BuildServiceProvider();
        return await CliCommandHandlers.RunAsync(provider, configuration, args);
    }
}

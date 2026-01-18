using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Unfollowed.App.Composition;
using Unfollowed.App.ViewModels;

namespace Unfollowed.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddUnfollowedCore();
        services.AddUnfollowedCsv();
        services.AddUnfollowedApp();
        services.AddUnfollowedRuntimeStubs(configuration);
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DataTabViewModel>();
        services.AddSingleton<ScanningTabViewModel>();
        services.AddSingleton<DiagnosticsTabViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        var window = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

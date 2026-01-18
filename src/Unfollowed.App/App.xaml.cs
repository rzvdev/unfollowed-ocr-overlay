using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Unfollowed.App.ViewModels;

namespace Unfollowed.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
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

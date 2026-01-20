using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Unfollowed.App.Composition;
using Unfollowed.App.Services;
using Unfollowed.App.Settings;
using Unfollowed.App.ViewModels;
using Unfollowed.Overlay;

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
        services.AddSingleton<AppSettingsStore>();
        services.AddSingleton(sp =>
        {
            var defaults = BuildDefaultSettings(configuration);
            return sp.GetRequiredService<AppSettingsStore>().Load(defaults);
        });
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DataTabViewModel>();
        services.AddSingleton<ScanningTabViewModel>();
        services.AddSingleton<DiagnosticsTabViewModel>();
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        ApplyTheme(_serviceProvider);

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = window;

        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if(_serviceProvider != null)
            await _serviceProvider.DisposeAsync();
        base.OnExit(e);
    }

    private static void ApplyTheme(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<AppSettings>();
        var themeService = serviceProvider.GetRequiredService<IThemeService>();
        themeService.ApplyTheme(settings.ThemeMode);
    }

    private static AppSettings BuildDefaultSettings(IConfiguration configuration)
    {
        return new AppSettings(
            TargetFps: configuration.GetValue("Scan:TargetFps", 4),
            OcrFrameDiffThreshold: configuration.GetValue("Scan:OcrFrameDiffThreshold", 0.02f),
            OcrMinTokenConfidence: configuration.GetValue("Ocr:MinTokenConfidence", 0.0f),
            StabilizerConfidenceThreshold: configuration.GetValue("Stabilizer:ConfidenceThreshold", 0.70f),
            Roi: null,
            Theme: configuration.GetValue("Overlay:Theme", OverlayTheme.Lime),
            ThemeMode: configuration.GetValue("App:ThemeMode", ThemeMode.System),
            ShowRoiOutline: configuration.GetValue("Overlay:ShowRoiOutline", false)
        );
    }
}

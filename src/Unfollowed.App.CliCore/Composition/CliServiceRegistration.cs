using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unfollowed.App.Scan;
using Unfollowed.App.Services;
using Unfollowed.Capture;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

namespace Unfollowed.App.CliCore.Composition;

public static class CliServiceRegistration
{
    public static IServiceCollection AddUnfollowedCliRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<IScanSessionController, ScanSessionController>();
        services.Replace(ServiceDescriptor.Singleton<IOverlayService, CliOverlayService>());
        services.TryAddSingleton<IFrameCapture, NullFrameCapture>();
        services.TryAddSingleton<IFramePreprocessor, BasicFramePreprocessor>();
        services.TryAddSingleton<IOcrProvider, NullOcrProvider>();
        services.TryAddSingleton<IOverlayRenderer, NullOverlayRenderer>();

        return services;
    }
}

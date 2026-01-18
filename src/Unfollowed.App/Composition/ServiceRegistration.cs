using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Unfollowed.App.Scan;
using Unfollowed.Capture;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Normalization;
using Unfollowed.Core.Stabilization;
using Unfollowed.Csv;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Overlay.Win32;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Composition
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddUnfollowedCore(this IServiceCollection services)
        {
            services.AddSingleton(new UsernameNormalizationOptions());
            services.AddSingleton<IUsernameNormalizer, UsernameNormalizer>();

            services.AddSingleton<IUsernameExtractor, RegexUsernameExtractor>();
            services.AddSingleton<IHighlightStabilizer, KOfMHighlightStabilizer>();

            return services;
        }

        public static IServiceCollection AddUnfollowedCsv(this IServiceCollection services)
        {
            services.AddSingleton<ICsvImporter, SimpleCsvImporter>();
            services.AddSingleton<INonFollowBackCalculator, NonFollowBackCalculator>();

            return services;
        }

        public static IServiceCollection AddUnfollowedApp(this IServiceCollection services)
        {
            services.AddSingleton<IScanSessionController, ScanSessionController>();
            return services;
        }

        public static IServiceCollection AddUnfollowedRuntimeStubs(this IServiceCollection services, IConfiguration configuration)
        {
            var useNullCapture = configuration.GetValue("Capture:UseNullFrameCapture", false);

            if (useNullCapture)
            {
                services.AddSingleton<IFrameCapture, NullFrameCapture>();
            }
            else
            {
                services.AddSingleton<IFrameCapture, Win32FrameCapture>();
            }
            services.AddSingleton<IFramePreprocessor, BasicFramePreprocessor>();
            services.AddSingleton<IOcrProvider, WindowsOcrProvider>();
            //services.AddSingleton<IOverlayRenderer, NullOverlayRenderer>();
            //services.AddSingleton<IOverlayRenderer, Win32OverlayRenderer>();
            services.AddTransient<IOverlayRenderer, Win32OverlayRenderer>();
            services.AddSingleton<IRoiSelector, Win32RoiSelector>();

            return services;
        }
    }
}

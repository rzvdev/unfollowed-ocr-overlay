using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unfollowed.App.Composition;
using Unfollowed.App.Scan;
using Unfollowed.Capture;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Models;
using Unfollowed.Core.Stabilization;
using Unfollowed.Csv;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

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
            .AddUnfollowedRuntimeStubs();

        services.AddSingleton<IScanSessionController, ScanSessionController>();

        var provider = services.BuildServiceProvider();
        var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Main");

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "compute":
                    return Compute(provider, args);
                case "scan":
                    return await ScanAsync(provider);
                default:
                    PrintHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception");
            return 2;
        }
    }

    private static int Compute(ServiceProvider provider, string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: compute <following.csv> <followers.csv>");
            return 1;
        }

        var importer = provider.GetRequiredService<ICsvImporter>();
        var calc = provider.GetRequiredService<INonFollowBackCalculator>();

        var following = importer.ImportUsernames(args[1], new CsvImportOptions(), CancellationToken.None);
        var followers = importer.ImportUsernames(args[2], new CsvImportOptions(), CancellationToken.None);
        var data = calc.Compute(following, followers);

        Console.WriteLine($"Following: {data.Following.Count}");
        Console.WriteLine($"Followers: {data.Followers.Count}");
        Console.WriteLine($"NonFollowBack: {data.NonFollowBack.Count}");

        return 0;
    }

    private static async Task<int> ScanAsync(ServiceProvider provider)
    {
        // Skeleton scan uses stubs for ROI selection, capture, OCR and overlay.
        // Replace stubs with Windows implementations to enable real-time highlighting.

        var roiSelector = provider.GetRequiredService<IRoiSelector>();
        var controller = provider.GetRequiredService<IScanSessionController>();

        var roi = await roiSelector.SelectRegionAsync(CancellationToken.None);

        var data = new NonFollowBackData(
            Following: Array.Empty<string>(),
            Followers: Array.Empty<string>(),
            NonFollowBack: Array.Empty<string>(),
            FollowingStats: new CsvImportStats(0, 0, 0, 0),
            FollowersStats: new CsvImportStats(0, 0, 0, 0)
        );

        var options = new ScanSessionOptions(
            TargetFps: 4,
            Preprocess: new PreprocessOptions(),
            Ocr: new OcrOptions(),
            Extraction: new ExtractionOptions(),
            Stabilizer: new StabilizerOptions(),
            Overlay: new OverlayOptions()
        );

        await controller.StartAsync(data, roi, options, CancellationToken.None);

        Console.WriteLine("Scanning started (stubs). Press ENTER to stop...");
        Console.ReadLine();

        await controller.StopAsync(CancellationToken.None);
        return 0;
    }


    private static void PrintHelp()
    {
        Console.WriteLine("GUI Unfollowed (OCR) - Skeleton");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  compute <following.csv> <followers.csv>   Compute NonFollowBack counts");
        Console.WriteLine("  scan                                      Start scan loop (stubs)");
        Console.WriteLine();
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Normalization;
using Unfollowed.Core.Stabilization;
using Unfollowed.Csv;
using Unfollowed.Ocr;
using Unfollowed.Preprocess;

namespace Unfollowed.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            b.SetMinimumLevel(LogLevel.Information);
        });

        services.AddSingleton(new UsernameNormalizationOptions());
        services.AddSingleton<IUsernameNormalizer, UsernameNormalizer>();
        services.AddSingleton<IUsernameExtractor, RegexUsernameExtractor>();
        services.AddSingleton<IHighlightStabilizer, KOfMHighlightStabilizer>();

        services.AddSingleton<ICsvImporter, SimpleCsvImporter>();
        services.AddSingleton<INonFollowBackCalculator, NonFollowBackCalculator>();

        services.AddSingleton<IFramePreprocessor, BasicFramePreprocessor>();
        services.AddSingleton<IOcrProvider, NullOcrProvider>();

        using var provider = services.BuildServiceProvider();
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
                case "convert-json":
                    return ConvertJsonToCsv(args);
                case "scan":
                case "scan-csv":
                case "settings":
                case "overlay-test":
                case "overlay-calibrate":
                case "capture-test":
                case "ocr-test":
                    return UnsupportedCommand(args[0]);
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

        var followingPath = args[1];
        var followersPath = args[2];

        var importer = provider.GetRequiredService<ICsvImporter>();
        var calc = provider.GetRequiredService<INonFollowBackCalculator>();

        var following = importer.ImportUsernames(followingPath, new CsvImportOptions(), CancellationToken.None);
        var followers = importer.ImportUsernames(followersPath, new CsvImportOptions(), CancellationToken.None);
        var data = calc.Compute(following, followers);

        PrintImportStats("Following", following);
        PrintImportStats("Followers", followers);

        Console.WriteLine($"Following: {data.Following.Count}");
        Console.WriteLine($"Followers: {data.Followers.Count}");
        Console.WriteLine($"NonFollowBack: {data.NonFollowBack.Count}");

        return 0;
    }

    private static int ConvertJsonToCsv(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: convert-json <following.json> <followers.json> <output-directory>");
            return 1;
        }

        var exporter = new InstagramJsonCsvExporter();
        exporter.Export(args[1], args[2], args[3], CancellationToken.None);

        Console.WriteLine($"Created: {Path.Combine(args[3], "following.csv")}");
        Console.WriteLine($"Created: {Path.Combine(args[3], "followers.csv")}");

        return 0;
    }

    private static int UnsupportedCommand(string command)
    {
        Console.Error.WriteLine($"Command '{command}' is not supported in the cross-platform CLI build.");
        Console.Error.WriteLine("Use the Windows UI app for capture/overlay/scan functionality.");
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Unfollowed CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  compute <following.csv> <followers.csv>   Compute NonFollowBack counts");
        Console.WriteLine("  scan                                      Start scan loop (not supported in CLI)");
        Console.WriteLine("  scan-csv <following.csv> <followers.csv>  Start scan loop with CSV input (not supported in CLI)");
        Console.WriteLine("  convert-json <following.json> <followers.json> <output-dir>  Export CSVs from Instagram JSON");
        Console.WriteLine("  settings                                  Configure stored settings (not supported in CLI)");
        Console.WriteLine("  overlay-test [x y w h]                    Show click-through overlay (not supported in CLI)");
        Console.WriteLine("  overlay-calibrate [x y w h]               Show ROI border + guides (not supported in CLI)");
        Console.WriteLine("  capture-test [x y w h] [count] [--preprocess]  Capture ROI frames (not supported in CLI)");
        Console.WriteLine("  ocr-test [x y w h]                        Run capture/preprocess/OCR (not supported in CLI)");
        Console.WriteLine();
    }

    private static void PrintImportStats(string label, CsvImportResult result)
    {
        Console.WriteLine($"{label} import:");
        Console.WriteLine($"  rows: {result.Stats.TotalRows}");
        Console.WriteLine($"  valid: {result.Stats.ValidUsernames}");
        Console.WriteLine($"  invalid: {result.Stats.InvalidRows}");
        Console.WriteLine($"  duplicates: {result.Stats.DuplicatesIgnored}");
        if (!string.IsNullOrWhiteSpace(result.DetectedUsernameColumn))
        {
            Console.WriteLine($"  detected column: {result.DetectedUsernameColumn}");
        }
    }
}

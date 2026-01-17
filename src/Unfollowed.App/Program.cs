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
            .AddUnfollowedRuntimeStubs(configuration);

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
                case "overlay-test":
                    return await OverlayTestAsync(provider, args);
                case "capture-test":
                    return await CaptureTestAsync(provider, args);
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

    private static async Task<int> OverlayTestAsync(ServiceProvider provider, string[] args)
    {
        // Usage:
        // overlay-test [x y w h]
        //
        // Examples:
        // overlay-test
        // overlay-test 200 150 800 600

        var controller = provider.GetRequiredService<IScanSessionController>();

        // Default ROI if not provided.
        var roi = new RoiSelection(200, 150, 800, 600);

        if (args.Length == 5
            && int.TryParse(args[1], out var x)
            && int.TryParse(args[2], out var y)
            && int.TryParse(args[3], out var w)
            && int.TryParse(args[4], out var h))
        {
            roi = new RoiSelection(x, y, w, h);
        }

        // Minimal dummy data – overlay test does not require CSV.
        var data = new NonFollowBackData(
            Following: Array.Empty<string>(),
            Followers: Array.Empty<string>(),
            NonFollowBack: Array.Empty<string>(),
            FollowingStats: new CsvImportStats(0, 0, 0, 0),
            FollowersStats: new CsvImportStats(0, 0, 0, 0)
        );

        // Overlay-focused options: make sure click-through is enabled.
        var options = new ScanSessionOptions(
            TargetFps: 1,
            Preprocess: new PreprocessOptions(),
            Ocr: new OcrOptions(),
            Extraction: new ExtractionOptions(),
            Stabilizer: new StabilizerOptions(),
            Overlay: new OverlayOptions(
                AlwaysOnTop: true,
                ClickThrough: true,
                ShowBadgeText: true
            )
        );

        await controller.StartAsync(data, roi, options, CancellationToken.None);

        Console.WriteLine("Overlay test started.");
        Console.WriteLine($"ROI: X={roi.X}, Y={roi.Y}, W={roi.Width}, H={roi.Height}");
        Console.WriteLine("You should see 2–3 green boxes inside the ROI window.");
        Console.WriteLine("Try clicking/scrolling in apps underneath (overlay must be click-through).");
        Console.WriteLine("Press ENTER to stop...");
        Console.ReadLine();

        await controller.StopAsync(CancellationToken.None);
        return 0;
    }

    private static async Task<int> CaptureTestAsync(ServiceProvider provider, string[] args)
    {
        // Usage:
        // capture-test [x y w h] [count] [--preprocess]
        //
        // Examples:
        // capture-test
        // capture-test 3
        // capture-test 200 150 800 600
        // capture-test 200 150 800 600 2
        // capture-test 200 150 800 600 2 --preprocess

        var roi = new RoiSelection(200, 150, 800, 600);
        var count = 3;
        var dumpPreprocessed = args.Any(arg => IsPreprocessFlag(arg));
        var filteredArgs = args.Where(arg => !IsPreprocessFlag(arg)).ToArray();

        if (filteredArgs.Length == 2 && int.TryParse(filteredArgs[1], out var parsedCount))
        {
            count = parsedCount;
        }
        else if (filteredArgs.Length >= 5
            && int.TryParse(filteredArgs[1], out var x)
            && int.TryParse(filteredArgs[2], out var y)
            && int.TryParse(filteredArgs[3], out var w)
            && int.TryParse(filteredArgs[4], out var h))
        {
            roi = new RoiSelection(x, y, w, h);

            if (filteredArgs.Length >= 6 && int.TryParse(filteredArgs[5], out parsedCount))
            {
                count = parsedCount;
            }
        }

        count = Math.Clamp(count, 1, 3);

        var capture = provider.GetRequiredService<IFrameCapture>();
        var preprocessor = provider.GetRequiredService<IFramePreprocessor>();
        await capture.InitializeAsync(roi, CancellationToken.None);

        try
        {
            for (var i = 0; i < count; i++)
            {
                var frame = await capture.CaptureAsync(CancellationToken.None);
                var filename = $"capture_{i + 1}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.bmp";
                var path = Path.Combine(Environment.CurrentDirectory, filename);
                SaveBgra32AsBmp(path, frame);
                Console.WriteLine($"Saved frame {i + 1}/{count} to {path}");

                if (dumpPreprocessed)
                {
                    var processed = preprocessor.Process(frame, new PreprocessOptions());
                    var preprocessedName = $"capture_{i + 1}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_gray8.bmp";
                    var preprocessedPath = Path.Combine(Environment.CurrentDirectory, preprocessedName);
                    SaveGray8AsBmp(preprocessedPath, processed);
                    Console.WriteLine($"Saved preprocessed frame {i + 1}/{count} to {preprocessedPath}");
                }
            }
        }
        finally
        {
            await capture.DisposeAsync();
        }

        return 0;
    }

    private static void SaveBgra32AsBmp(string path, CaptureFrame frame)
    {
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        var imageSize = frame.Width * frame.Height * 4;

        if (frame.Bgra32.Length < imageSize)
        {
            throw new InvalidOperationException("Frame buffer is smaller than expected.");
        }

        var offset = fileHeaderSize + infoHeaderSize;
        var fileSize = offset + imageSize;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0x4D42);
        writer.Write(fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(offset);

        writer.Write(infoHeaderSize);
        writer.Write(frame.Width);
        writer.Write(-frame.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        writer.Write(frame.Bgra32, 0, imageSize);
    }

    private static void SaveGray8AsBmp(string path, ProcessedFrame frame)
    {
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        const int paletteSize = 256 * 4;
        var stride = (frame.Width + 3) & ~3;
        var imageSize = stride * frame.Height;
        var offset = fileHeaderSize + infoHeaderSize + paletteSize;
        var fileSize = offset + imageSize;

        if (frame.Gray8.Length < frame.Width * frame.Height)
        {
            throw new InvalidOperationException("Processed buffer is smaller than expected.");
        }

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0x4D42);
        writer.Write(fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(offset);

        writer.Write(infoHeaderSize);
        writer.Write(frame.Width);
        writer.Write(-frame.Height);
        writer.Write((ushort)1);
        writer.Write((ushort)8);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(256);
        writer.Write(0);

        for (var i = 0; i < 256; i++)
        {
            writer.Write((byte)i);
            writer.Write((byte)i);
            writer.Write((byte)i);
            writer.Write((byte)0);
        }

        var rowPadding = stride - frame.Width;
        var offsetIndex = 0;

        for (var y = 0; y < frame.Height; y++)
        {
            writer.Write(frame.Gray8, offsetIndex, frame.Width);
            offsetIndex += frame.Width;

            for (var p = 0; p < rowPadding; p++)
            {
                writer.Write((byte)0);
            }
        }
    }

    private static bool IsPreprocessFlag(string arg)
        => arg.Equals("--preprocess", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--gray", StringComparison.OrdinalIgnoreCase);

    private static void PrintHelp()
    {
        Console.WriteLine("GUI Unfollowed (OCR) - Skeleton");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  compute <following.csv> <followers.csv>   Compute NonFollowBack counts");
        Console.WriteLine("  scan                                      Start scan loop (stubs)");
        Console.WriteLine("  overlay-test [x y w h]                    Show click-through overlay and test alignment");
        Console.WriteLine("  capture-test [x y w h] [count] [--preprocess]  Capture 1-3 ROI frames to BMP on disk");
        Console.WriteLine();
    }
}

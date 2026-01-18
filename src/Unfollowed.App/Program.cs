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
using System.Windows.Forms;

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
                    return await ScanAsync(provider, configuration);
                case "scan-csv":
                    return await ScanWithCsvAsync(provider, configuration, args);
                case "convert-json":
                    return ConvertJsonToCsv(args);
                case "overlay-test":
                    return await OverlayTestAsync(provider, args);
                case "overlay-calibrate":
                    return await OverlayCalibrateAsync(provider, args);
                case "capture-test":
                    return await CaptureTestAsync(provider, args);
                case "ocr-test":
                    return await OcrTestAsync(provider, args);
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
        if (!TryResolveCsvInputs(args, out var followingPath, out var followersPath))
        {
            Console.Error.WriteLine("Usage: compute <following.csv> <followers.csv>");
            return 1;
        }

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

    private static async Task<int> ScanAsync(ServiceProvider provider, IConfiguration configuration)
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

        var options = BuildScanOptions(configuration);

        await controller.StartAsync(data, roi, options, CancellationToken.None);

        Console.WriteLine("Scanning started (stubs). Press ENTER to stop...");
        Console.ReadLine();

        await controller.StopAsync(CancellationToken.None);
        return 0;
    }

    private static async Task<int> ScanWithCsvAsync(ServiceProvider provider, IConfiguration configuration, string[] args)
    {
        if (!TryResolveCsvInputs(args, out var followingPath, out var followersPath))
        {
            Console.Error.WriteLine("Usage: scan-csv <following.csv> <followers.csv>");
            return 1;
        }

        var importer = provider.GetRequiredService<ICsvImporter>();
        var calculator = provider.GetRequiredService<INonFollowBackCalculator>();

        var following = importer.ImportUsernames(followingPath, new CsvImportOptions(), CancellationToken.None);
        var followers = importer.ImportUsernames(followersPath, new CsvImportOptions(), CancellationToken.None);
        var data = calculator.Compute(following, followers);

        PrintImportStats("Following", following);
        PrintImportStats("Followers", followers);

        Console.WriteLine($"Following: {data.Following.Count}");
        Console.WriteLine($"Followers: {data.Followers.Count}");
        Console.WriteLine($"NonFollowBack: {data.NonFollowBack.Count}");
        if (data.NonFollowBack.Count == 0)
        {
            Console.Error.WriteLine("No NonFollowBack entries found. Check your CSV inputs before scanning.");
            return 1;
        }

        var roiSelector = provider.GetRequiredService<IRoiSelector>();
        var controller = provider.GetRequiredService<IScanSessionController>();
        var roi = await roiSelector.SelectRegionAsync(CancellationToken.None);
        var options = BuildScanOptions(configuration);

        await controller.StartAsync(data, roi, options, CancellationToken.None);

        Console.WriteLine("Scanning started. Press ENTER to stop...");
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

    private static async Task<int> OverlayCalibrateAsync(ServiceProvider provider, string[] args)
    {
        // Usage:
        // overlay-calibrate [x y w h]
        //
        // Examples:
        // overlay-calibrate
        // overlay-calibrate 200 150 800 600

        var overlay = provider.GetRequiredService<IOverlayRenderer>();

        var roi = new RoiSelection(200, 150, 800, 600);

        if (args.Length == 5
            && int.TryParse(args[1], out var x)
            && int.TryParse(args[2], out var y)
            && int.TryParse(args[3], out var w)
            && int.TryParse(args[4], out var h))
        {
            roi = new RoiSelection(x, y, w, h);
        }

        var options = new OverlayOptions(
            AlwaysOnTop: true,
            ClickThrough: true,
            ShowBadgeText: false
        );

        await overlay.InitializeAsync(roi, options, CancellationToken.None);

        try
        {
            var highlights = BuildCalibrationHighlights(roi, 2f);
            await overlay.RenderAsync(highlights, CancellationToken.None);

            Console.WriteLine("Overlay calibration started.");
            Console.WriteLine($"ROI: X={roi.X}, Y={roi.Y}, W={roi.Width}, H={roi.Height}");
            Console.WriteLine("Verify borders, center lines, and third guides align with the intended ROI.");
            Console.WriteLine("Press ENTER to stop...");
            Console.ReadLine();
        }
        finally
        {
            await overlay.ClearAsync(CancellationToken.None);
            await overlay.DisposeAsync();
        }

        return 0;
    }

    private static async Task<int> OcrTestAsync(ServiceProvider provider, string[] args)
    {
        // Usage:
        // ocr-test [x y w h]
        //
        // Examples:
        // ocr-test
        // ocr-test 200 150 800 600

        var roi = new RoiSelection(200, 150, 800, 600);

        if (args.Length == 5
            && int.TryParse(args[1], out var x)
            && int.TryParse(args[2], out var y)
            && int.TryParse(args[3], out var w)
            && int.TryParse(args[4], out var h))
        {
            roi = new RoiSelection(x, y, w, h);
        }

        var capture = provider.GetRequiredService<IFrameCapture>();
        var preprocessor = provider.GetRequiredService<IFramePreprocessor>();
        var ocr = provider.GetRequiredService<IOcrProvider>();

        await capture.InitializeAsync(roi, CancellationToken.None);

        try
        {
            var frame = await capture.CaptureAsync(CancellationToken.None);
            var processed = preprocessor.Process(frame, new PreprocessOptions());
            var result = await ocr.RecognizeAsync(processed, new OcrOptions(), CancellationToken.None);

            Console.WriteLine($"OCR tokens: {result.Tokens.Count}");

            foreach (var token in result.Tokens)
            {
                Console.WriteLine($"{token.Text} | conf={token.Confidence:0.00} | roi=({token.RoiRect.X:0.0},{token.RoiRect.Y:0.0},{token.RoiRect.W:0.0},{token.RoiRect.H:0.0})");
            }

            var confidenceGroups = result.Tokens
                .GroupBy(token => MathF.Round(token.Confidence, 2))
                .OrderBy(group => group.Key);

            Console.WriteLine("Confidence counts:");
            foreach (var group in confidenceGroups)
            {
                Console.WriteLine($"{group.Key:0.00}: {group.Count()}");
            }
        }
        finally
        {
            await capture.DisposeAsync();
        }

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
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("CaptureTest");
        await capture.InitializeAsync(roi, CancellationToken.None);

        try
        {
            for (var i = 0; i < count; i++)
            {
                var frame = await capture.CaptureAsync(CancellationToken.None);
                var filename = $"capture_{i + 1}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.bmp";
                var pathExists = Directory.Exists(Environment.CurrentDirectory + "/captures/");
                if (!pathExists)
                {
                    Directory.CreateDirectory(Environment.CurrentDirectory + "/captures/");
                }
                var path = Path.Combine(Environment.CurrentDirectory + "/captures/", filename);
                SaveBgra32AsBmp(path, frame);
                Console.WriteLine($"Saved frame {i + 1}/{count} to {path}");
                ValidateCaptureBmp(path, roi, frame, logger);

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
        const int pelsPerMeter = 3780; // 96 DPI
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
        writer.Write(pelsPerMeter);
        writer.Write(pelsPerMeter);
        writer.Write(0);
        writer.Write(0);

        writer.Write(frame.Bgra32, 0, imageSize);
    }

    private static void SaveGray8AsBmp(string path, ProcessedFrame frame)
    {
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        const int paletteSize = 256 * 4;
        const int pelsPerMeter = 3780; // 96 DPI
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
        writer.Write(pelsPerMeter);
        writer.Write(pelsPerMeter);
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

    private static void ValidateCaptureBmp(string path, RoiSelection roi, CaptureFrame frame, ILogger logger)
    {
        var info = ReadBmpInfo(path);
        var dpiX = PixelsPerMeterToDpi(info.XPelsPerMeter);
        var dpiY = PixelsPerMeterToDpi(info.YPelsPerMeter);
        logger.LogInformation(
            "BMP DPI: {XPelsPerMeter} ppm ({DpiX:0.##} dpi), {YPelsPerMeter} ppm ({DpiY:0.##} dpi)",
            info.XPelsPerMeter,
            dpiX,
            info.YPelsPerMeter,
            dpiY);

        if (info.Width != roi.Width || info.Height != roi.Height)
        {
            logger.LogWarning(
                "BMP dimensions mismatch. Expected ROI {ExpectedWidth}x{ExpectedHeight}, got {ActualWidth}x{ActualHeight}.",
                roi.Width,
                roi.Height,
                info.Width,
                info.Height);
        }

        if (!info.IsTopDown)
        {
            logger.LogWarning("BMP orientation mismatch. Expected top-down bitmap (negative height).");
        }

        var bmpData = ReadBmpPixelData(path, info);
        if (bmpData.Length != frame.Bgra32.Length)
        {
            logger.LogWarning(
                "BMP pixel data length mismatch. Expected {ExpectedLength} bytes, got {ActualLength} bytes.",
                frame.Bgra32.Length,
                bmpData.Length);
            return;
        }

        var mismatchCount = 0;
        for (var i = 0; i < bmpData.Length; i++)
        {
            if (bmpData[i] != frame.Bgra32[i])
            {
                mismatchCount++;
            }
        }

        if (mismatchCount > 0)
        {
            logger.LogWarning(
                "BMP pixel data mismatch detected: {MismatchCount} bytes differ out of {TotalBytes}.",
                mismatchCount,
                bmpData.Length);
        }
    }

    private static BmpInfo ReadBmpInfo(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        var signature = reader.ReadUInt16();
        if (signature != 0x4D42)
        {
            throw new InvalidOperationException("Invalid BMP signature.");
        }

        reader.ReadInt32(); // file size
        reader.ReadUInt16(); // reserved1
        reader.ReadUInt16(); // reserved2
        var dataOffset = reader.ReadInt32();
        var infoHeaderSize = reader.ReadInt32();
        if (infoHeaderSize < 40)
        {
            throw new InvalidOperationException("Unsupported BMP header size.");
        }

        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        reader.ReadUInt16(); // planes
        var bitCount = reader.ReadUInt16();
        reader.ReadInt32(); // compression
        var imageSize = reader.ReadInt32();
        var xPelsPerMeter = reader.ReadInt32();
        var yPelsPerMeter = reader.ReadInt32();
        reader.ReadInt32(); // clr used
        reader.ReadInt32(); // clr important

        var isTopDown = height < 0;
        var actualHeight = Math.Abs(height);

        return new BmpInfo(
            width,
            actualHeight,
            bitCount,
            imageSize,
            xPelsPerMeter,
            yPelsPerMeter,
            dataOffset,
            isTopDown);
    }

    private static byte[] ReadBmpPixelData(string path, BmpInfo info)
    {
        var bytesToRead = info.ImageSize;
        if (bytesToRead <= 0)
        {
            bytesToRead = info.BitCount switch
            {
                32 => info.Width * info.Height * 4,
                8 => ((info.Width + 3) & ~3) * info.Height,
                _ => throw new InvalidOperationException("Unsupported BMP format for pixel read.")
            };
        }

        using var stream = File.OpenRead(path);
        stream.Seek(info.DataOffset, SeekOrigin.Begin);
        var buffer = new byte[bytesToRead];
        var read = stream.Read(buffer, 0, buffer.Length);
        if (read != bytesToRead)
        {
            Array.Resize(ref buffer, read);
        }
        return buffer;
    }

    private static float PixelsPerMeterToDpi(int pelsPerMeter)
        => pelsPerMeter <= 0 ? 0 : pelsPerMeter / 39.3701f;

    private sealed record BmpInfo(
        int Width,
        int Height,
        int BitCount,
        int ImageSize,
        int XPelsPerMeter,
        int YPelsPerMeter,
        int DataOffset,
        bool IsTopDown);

    private static bool IsPreprocessFlag(string arg)
        => arg.Equals("--preprocess", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--gray", StringComparison.OrdinalIgnoreCase);

    private static ScanSessionOptions BuildScanOptions(IConfiguration configuration)
    {
        var targetFps = configuration.GetValue("Scan:TargetFps", 4);

        var defaultPreprocessOptions = new PreprocessOptions(
            Profile: configuration.GetValue("Preprocess:Profile", PreprocessProfile.Default),
            Contrast: configuration.GetValue("Preprocess:Contrast", 1.0f),
            Sharpen: configuration.GetValue("Preprocess:Sharpen", 0.0f)
        );
        var selectedPreprocessProfile = configuration.GetValue<string?>("Preprocess:ProfileName");
        var namedPreprocessProfiles = configuration
            .GetSection("Preprocess:Profiles")
            .Get<Dictionary<string, PreprocessOptions>>();
        var preprocessOptions = PreprocessProfileCatalog.Resolve(
            selectedPreprocessProfile,
            namedPreprocessProfiles,
            defaultPreprocessOptions);

        var ocrOptions = new OcrOptions(
            LanguageTag: configuration.GetValue("Ocr:LanguageTag", "en"),
            MinTokenConfidence: configuration.GetValue("Ocr:MinTokenConfidence", 0.0f),
            CharacterWhitelist: configuration.GetValue<string?>("Ocr:CharacterWhitelist", "abcdefghijklmnopqrstuvwxyz0123456789._@"),
            AssumedTokenConfidence: configuration.GetValue("Ocr:AssumedTokenConfidence", 0.85f)
        );

        var stabilizerOptions = new StabilizerOptions(
            WindowSizeM: configuration.GetValue("Stabilizer:WindowSizeM", 5),
            RequiredK: configuration.GetValue("Stabilizer:RequiredK", 3),
            ConfidenceThreshold: configuration.GetValue("Stabilizer:ConfidenceThreshold", 0.70f),
            AllowUncertainHighlights: configuration.GetValue("Stabilizer:AllowUncertainHighlights", false)
        );

        return new ScanSessionOptions(
            TargetFps: targetFps,
            Preprocess: preprocessOptions,
            Ocr: ocrOptions,
            Extraction: new ExtractionOptions(),
            Stabilizer: stabilizerOptions,
            Overlay: new OverlayOptions()
        );
    }

    private static IReadOnlyList<Highlight> BuildCalibrationHighlights(RoiSelection roi, float thickness)
    {
        var minDimension = MathF.Min(roi.Width, roi.Height);
        var inset = MathF.Max(thickness * 2f, minDimension * 0.05f);
        var left = roi.X;
        var top = roi.Y;
        var right = roi.X + roi.Width;
        var bottom = roi.Y + roi.Height;
        var maxInset = MathF.Max(thickness, minDimension / 2f - thickness);
        inset = MathF.Min(inset, maxInset);
        var innerWidth = MathF.Max(1f, roi.Width - inset * 2);
        var innerHeight = MathF.Max(1f, roi.Height - inset * 2);
        var innerLeft = left + inset;
        var innerTop = top + inset;
        var centerX = left + roi.Width / 2f;
        var centerY = top + roi.Height / 2f;
        var thirdX = left + roi.Width / 3f;
        var twoThirdX = left + roi.Width * 2f / 3f;
        var thirdY = top + roi.Height / 3f;
        var twoThirdY = top + roi.Height * 2f / 3f;

        var highlights = new List<Highlight>
        {
            new("calibration_border_top", 1f, new RectF(left, top, roi.Width, thickness), true),
            new("calibration_border_bottom", 1f, new RectF(left, bottom - thickness, roi.Width, thickness), true),
            new("calibration_border_left", 1f, new RectF(left, top, thickness, roi.Height), true),
            new("calibration_border_right", 1f, new RectF(right - thickness, top, thickness, roi.Height), true),
            new("calibration_inset_top", 1f, new RectF(innerLeft, innerTop, innerWidth, thickness), true),
            new("calibration_inset_bottom", 1f, new RectF(innerLeft, bottom - inset - thickness, innerWidth, thickness), true),
            new("calibration_inset_left", 1f, new RectF(innerLeft, innerTop, thickness, innerHeight), true),
            new("calibration_inset_right", 1f, new RectF(right - inset - thickness, innerTop, thickness, innerHeight), true),
            new("calibration_center_h", 1f, new RectF(innerLeft, centerY - thickness / 2f, innerWidth, thickness), true),
            new("calibration_center_v", 1f, new RectF(centerX - thickness / 2f, innerTop, thickness, innerHeight), true),
            new("calibration_third_h", 1f, new RectF(innerLeft, thirdY - thickness / 2f, innerWidth, thickness), true),
            new("calibration_two_third_h", 1f, new RectF(innerLeft, twoThirdY - thickness / 2f, innerWidth, thickness), true),
            new("calibration_third_v", 1f, new RectF(thirdX - thickness / 2f, innerTop, thickness, innerHeight), true),
            new("calibration_two_third_v", 1f, new RectF(twoThirdX - thickness / 2f, innerTop, thickness, innerHeight), true)
        };

        return highlights;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("GUI Unfollowed (OCR) - Skeleton");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  compute <following.csv> <followers.csv>   Compute NonFollowBack counts");
        Console.WriteLine("  scan                                      Start scan loop (stubs)");
        Console.WriteLine("  scan-csv <following.csv> <followers.csv>  Start scan loop with CSV input");
        Console.WriteLine("  convert-json <following.json> <followers.json> <output-dir>  Export CSVs from Instagram JSON");
        Console.WriteLine("  overlay-test [x y w h]                    Show click-through overlay and test alignment");
        Console.WriteLine("  overlay-calibrate [x y w h]               Show ROI border + guides for calibration");
        Console.WriteLine("  capture-test [x y w h] [count] [--preprocess]  Capture 1-3 ROI frames to BMP on disk");
        Console.WriteLine("  ocr-test [x y w h]                        Run capture/preprocess/OCR once and print tokens");
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

    private static bool TryResolveCsvInputs(string[] args, out string followingPath, out string followersPath)
    {
        if (args.Length >= 3)
        {
            followingPath = args[1];
            followersPath = args[2];
            return true;
        }

        followingPath = string.Empty;
        followersPath = string.Empty;

        var followingSelection = PromptForCsvPath("Select following.csv");
        if (string.IsNullOrWhiteSpace(followingSelection))
        {
            return false;
        }

        var followersSelection = PromptForCsvPath("Select followers.csv");
        if (string.IsNullOrWhiteSpace(followersSelection))
        {
            return false;
        }

        followingPath = followingSelection;
        followersPath = followersSelection;
        return true;
    }

    private static string? PromptForCsvPath(string title)
    {
        string? selection = null;
        var dialogThread = new Thread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                Title = title,
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                selection = dialog.FileName;
            }
        });

        dialogThread.SetApartmentState(ApartmentState.STA);
        dialogThread.Start();
        dialogThread.Join();

        return selection;
    }
}

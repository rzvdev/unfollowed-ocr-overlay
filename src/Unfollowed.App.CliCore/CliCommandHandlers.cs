using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unfollowed.App.Scan;
using Unfollowed.App.Settings;
using Unfollowed.Capture;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Models;
using Unfollowed.Core.Stabilization;
using Unfollowed.Csv;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;
using System.Globalization;
using System.IO;

namespace Unfollowed.App.CliCore;

public static class CliCommandHandlers
{
    public static async Task<int> RunAsync(ServiceProvider provider, IConfiguration configuration, string[] args)
    {
        var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Main");

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var command = args[0].ToLowerInvariant();
            var parsed = CliArguments.Parse(args.Skip(1).ToArray());

            return command switch
            {
                "compute" => Compute(provider, parsed),
                "scan" => await ScanAsync(provider, configuration, parsed),
                "scan-csv" => await ScanWithCsvAsync(provider, configuration, parsed),
                "convert-json" => ConvertJsonToCsv(parsed),
                "settings" => SettingsAsync(configuration, parsed),
                "overlay-test" => await OverlayTestAsync(provider, parsed),
                "overlay-calibrate" => await OverlayCalibrateAsync(provider, parsed),
                "capture-test" => await CaptureTestAsync(provider, parsed),
                "ocr-test" => await OcrTestAsync(provider, parsed),
                _ => PrintHelpAndReturnError()
            };
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception");
            return 2;
        }
    }

    private static int Compute(ServiceProvider provider, CliArguments parsed)
    {
        if (parsed.Positionals.Count < 2)
        {
            Console.Error.WriteLine("Missing CSV inputs. Usage: compute <following.csv> <followers.csv>");
            return 1;
        }

        var followingPath = parsed.Positionals[0];
        var followersPath = parsed.Positionals[1];

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

    private static int ConvertJsonToCsv(CliArguments parsed)
    {
        if (parsed.Positionals.Count < 3)
        {
            Console.Error.WriteLine("Missing JSON inputs. Usage: convert-json <following.json> <followers.json> <output-directory>");
            return 1;
        }

        var exporter = new InstagramJsonCsvExporter();
        exporter.Export(parsed.Positionals[0], parsed.Positionals[1], parsed.Positionals[2], CancellationToken.None);

        Console.WriteLine($"Created: {Path.Combine(parsed.Positionals[2], "following.csv")}");
        Console.WriteLine($"Created: {Path.Combine(parsed.Positionals[2], "followers.csv")}");

        return 0;
    }

    private static async Task<int> ScanAsync(ServiceProvider provider, IConfiguration configuration, CliArguments parsed)
    {
        var settingsStore = new AppSettingsStore(parsed.GetOption("settings"));
        var defaults = BuildDefaultSettings(configuration);
        var settings = settingsStore.Load(defaults);

        if (!TryApplySettingsOverrides(settings, parsed, out var updatedSettings, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        if (updatedSettings.Roi is null)
        {
            Console.Error.WriteLine("ROI is required. Provide --roi x,y,w,h or set Roi in the settings file.");
            return 1;
        }

        var roi = updatedSettings.Roi;

        var data = new NonFollowBackData(
            Following: Array.Empty<string>(),
            Followers: Array.Empty<string>(),
            NonFollowBack: Array.Empty<string>(),
            FollowingStats: new CsvImportStats(0, 0, 0, 0),
            FollowersStats: new CsvImportStats(0, 0, 0, 0)
        );

        var controller = provider.GetRequiredService<IScanSessionController>();
        var options = BuildScanOptions(configuration, updatedSettings);

        settingsStore.Save(updatedSettings);

        await controller.StartAsync(data, roi, options, CancellationToken.None);

        Console.WriteLine("Scanning started (stubs). Press ENTER to stop...");
        Console.ReadLine();

        await controller.StopAsync(CancellationToken.None);
        return 0;
    }

    private static async Task<int> ScanWithCsvAsync(ServiceProvider provider, IConfiguration configuration, CliArguments parsed)
    {
        if (parsed.Positionals.Count < 2)
        {
            Console.Error.WriteLine("Missing CSV inputs. Usage: scan-csv <following.csv> <followers.csv> --roi x,y,w,h");
            return 1;
        }

        var importer = provider.GetRequiredService<ICsvImporter>();
        var calculator = provider.GetRequiredService<INonFollowBackCalculator>();

        var following = importer.ImportUsernames(parsed.Positionals[0], new CsvImportOptions(), CancellationToken.None);
        var followers = importer.ImportUsernames(parsed.Positionals[1], new CsvImportOptions(), CancellationToken.None);
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

        var settingsStore = new AppSettingsStore(parsed.GetOption("settings"));
        var defaults = BuildDefaultSettings(configuration);
        var settings = settingsStore.Load(defaults);

        if (!TryApplySettingsOverrides(settings, parsed, out var updatedSettings, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        if (updatedSettings.Roi is null)
        {
            Console.Error.WriteLine("ROI is required. Provide --roi x,y,w,h or set Roi in the settings file.");
            return 1;
        }

        var roi = updatedSettings.Roi;
        var controller = provider.GetRequiredService<IScanSessionController>();
        var options = BuildScanOptions(configuration, updatedSettings);

        settingsStore.Save(updatedSettings);

        await controller.StartAsync(data, roi, options, CancellationToken.None);

        Console.WriteLine("Scanning started. Press ENTER to stop...");
        Console.ReadLine();

        await controller.StopAsync(CancellationToken.None);
        return 0;
    }

    private static async Task<int> OverlayTestAsync(ServiceProvider provider, CliArguments parsed)
    {
        var controller = provider.GetRequiredService<IScanSessionController>();
        var roi = ResolveRoiWithDefault(parsed, new RoiSelection(200, 150, 800, 600));

        var data = new NonFollowBackData(
            Following: Array.Empty<string>(),
            Followers: Array.Empty<string>(),
            NonFollowBack: Array.Empty<string>(),
            FollowingStats: new CsvImportStats(0, 0, 0, 0),
            FollowersStats: new CsvImportStats(0, 0, 0, 0)
        );

        var options = new ScanSessionOptions(
            TargetFps: 1,
            OcrFrameDiffThreshold: 0.0f,
            Preprocess: new PreprocessOptions(),
            Ocr: new OcrOptions(),
            Extraction: new ExtractionOptions(),
            Stabilizer: new StabilizerOptions(),
            Overlay: new OverlayOptions(
                AlwaysOnTop: true,
                ClickThrough: true,
                ShowBadgeText: true
            ),
            CaptureDump: new CaptureDumpOptions()
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

    private static async Task<int> OverlayCalibrateAsync(ServiceProvider provider, CliArguments parsed)
    {
        var overlay = provider.GetRequiredService<IOverlayRenderer>();
        var roi = ResolveRoiWithDefault(parsed, new RoiSelection(200, 150, 800, 600));

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

    private static async Task<int> OcrTestAsync(ServiceProvider provider, CliArguments parsed)
    {
        var roi = ResolveRoiWithDefault(parsed, new RoiSelection(200, 150, 800, 600));

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

    private static async Task<int> CaptureTestAsync(ServiceProvider provider, CliArguments parsed)
    {
        var roi = ResolveRoiWithDefault(parsed, new RoiSelection(200, 150, 800, 600));
        var count = ResolveCaptureCount(parsed);
        var dumpPreprocessed = parsed.HasFlag("preprocess") || parsed.HasFlag("gray");

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

    private static bool TryApplySettingsOverrides(AppSettings settings, CliArguments parsed, out AppSettings updated, out string error)
    {
        updated = settings;
        error = string.Empty;

        if (parsed.TryGetOption("roi", out var roiText))
        {
            if (!TryParseRoi(roiText, out var roi))
            {
                error = "Invalid --roi value. Expected format: x,y,w,h";
                return false;
            }

            updated = updated with { Roi = roi };
        }

        if (parsed.TryGetOption("target-fps", out var targetFpsText))
        {
            if (!TryParseInt(targetFpsText, out var targetFps))
            {
                error = "Invalid --target-fps value.";
                return false;
            }

            updated = updated with { TargetFps = targetFps };
        }

        if (parsed.TryGetOption("ocr-frame-diff", out var diffText))
        {
            if (!TryParseFloat(diffText, out var diff))
            {
                error = "Invalid --ocr-frame-diff value.";
                return false;
            }

            updated = updated with { OcrFrameDiffThreshold = diff };
        }

        if (parsed.TryGetOption("ocr-min-confidence", out var minConfidenceText))
        {
            if (!TryParseFloat(minConfidenceText, out var minConfidence))
            {
                error = "Invalid --ocr-min-confidence value.";
                return false;
            }

            updated = updated with { OcrMinTokenConfidence = minConfidence };
        }

        if (parsed.TryGetOption("stabilizer-confidence", out var stabilizerText))
        {
            if (!TryParseFloat(stabilizerText, out var stabilizerConfidence))
            {
                error = "Invalid --stabilizer-confidence value.";
                return false;
            }

            updated = updated with { StabilizerConfidenceThreshold = stabilizerConfidence };
        }

        if (parsed.TryGetOption("theme", out var themeText))
        {
            if (!Enum.TryParse<OverlayTheme>(themeText, true, out var theme))
            {
                error = "Invalid --theme value. Expected Lime, Amber, or Cyan.";
                return false;
            }

            updated = updated with { Theme = theme };
        }

        if (parsed.TryGetOption("theme-mode", out var themeModeText))
        {
            if (!Enum.TryParse<ThemeMode>(themeModeText, true, out var themeMode))
            {
                error = "Invalid --theme-mode value. Expected Light, Dark, or System.";
                return false;
            }

            updated = updated with { ThemeMode = themeMode };
        }

        if (parsed.HasFlag("show-roi"))
        {
            updated = updated with { ShowRoiOutline = true };
        }

        if (parsed.HasFlag("hide-roi"))
        {
            updated = updated with { ShowRoiOutline = false };
        }

        return true;
    }

    private static int SettingsAsync(IConfiguration configuration, CliArguments parsed)
    {
        var store = new AppSettingsStore(parsed.GetOption("settings"));
        var defaults = BuildDefaultSettings(configuration);

        if (parsed.HasFlag("reset"))
        {
            store.Reset();
            Console.WriteLine("Settings reset to defaults.");
            return 0;
        }

        var current = store.Load(defaults);
        var hasOverrides = HasSettingsOverrides(parsed);

        if (!hasOverrides)
        {
            PrintSettings(current);
            Console.WriteLine("Use --target-fps, --ocr-frame-diff, --ocr-min-confidence, --stabilizer-confidence, --theme, --theme-mode, --show-roi, --hide-roi, or --roi to update settings.");
            return 0;
        }

        if (!TryApplySettingsOverrides(current, parsed, out var updated, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        store.Save(updated);
        Console.WriteLine("Settings saved.");
        return 0;
    }

    private static void PrintSettings(AppSettings settings)
    {
        Console.WriteLine("Settings");
        Console.WriteLine($"  TargetFps: {settings.TargetFps}");
        Console.WriteLine($"  OcrFrameDiffThreshold: {settings.OcrFrameDiffThreshold}");
        Console.WriteLine($"  OcrMinTokenConfidence: {settings.OcrMinTokenConfidence}");
        Console.WriteLine($"  StabilizerConfidenceThreshold: {settings.StabilizerConfidenceThreshold}");
        Console.WriteLine($"  Theme: {settings.Theme}");
        Console.WriteLine($"  ThemeMode: {settings.ThemeMode}");
        Console.WriteLine($"  ShowRoiOutline: {settings.ShowRoiOutline}");
        if (settings.Roi is null)
        {
            Console.WriteLine("  Roi: <unset>");
        }
        else
        {
            Console.WriteLine($"  Roi: {settings.Roi.X},{settings.Roi.Y},{settings.Roi.Width},{settings.Roi.Height}");
        }
    }

    private static bool HasSettingsOverrides(CliArguments parsed)
    {
        return parsed.HasOption("roi")
            || parsed.HasOption("target-fps")
            || parsed.HasOption("ocr-frame-diff")
            || parsed.HasOption("ocr-min-confidence")
            || parsed.HasOption("stabilizer-confidence")
            || parsed.HasOption("theme")
            || parsed.HasOption("theme-mode")
            || parsed.HasFlag("show-roi")
            || parsed.HasFlag("hide-roi");
    }

    private static bool TryParseRoi(string roiText, out RoiSelection roi)
    {
        roi = default;
        var parts = roiText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
        {
            return false;
        }

        roi = new RoiSelection(x, y, w, h);
        return true;
    }

    private static RoiSelection ResolveRoiWithDefault(CliArguments parsed, RoiSelection fallback)
    {
        if (parsed.TryGetOption("roi", out var roiText) && TryParseRoi(roiText, out var roi))
        {
            return roi;
        }

        if (parsed.Positionals.Count >= 4
            && int.TryParse(parsed.Positionals[0], out var x)
            && int.TryParse(parsed.Positionals[1], out var y)
            && int.TryParse(parsed.Positionals[2], out var w)
            && int.TryParse(parsed.Positionals[3], out var h))
        {
            return new RoiSelection(x, y, w, h);
        }

        return fallback;
    }

    private static int ResolveCaptureCount(CliArguments parsed)
    {
        if (parsed.TryGetOption("count", out var countText) && TryParseInt(countText, out var count))
        {
            return count;
        }

        if (parsed.Positionals.Count == 1 && TryParseInt(parsed.Positionals[0], out var positionalCount))
        {
            return positionalCount;
        }

        if (parsed.Positionals.Count >= 5 && TryParseInt(parsed.Positionals[4], out var trailingCount))
        {
            return trailingCount;
        }

        return 3;
    }

    private static bool TryParseInt(string input, out int value)
        => int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseFloat(string input, out float value)
        => float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static ScanSessionOptions BuildScanOptions(IConfiguration configuration, AppSettings settings)
    {
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
            MinTokenConfidence: settings.OcrMinTokenConfidence,
            CharacterWhitelist: configuration.GetValue<string?>("Ocr:CharacterWhitelist", "abcdefghijklmnopqrstuvwxyz0123456789._@"),
            AssumedTokenConfidence: configuration.GetValue("Ocr:AssumedTokenConfidence", 0.85f)
        );

        var stabilizerOptions = new StabilizerOptions(
            WindowSizeM: configuration.GetValue("Stabilizer:WindowSizeM", 5),
            RequiredK: configuration.GetValue("Stabilizer:RequiredK", 3),
            ConfidenceThreshold: settings.StabilizerConfidenceThreshold,
            AllowUncertainHighlights: configuration.GetValue("Stabilizer:AllowUncertainHighlights", false)
        );

        var captureDumpOptions = new CaptureDumpOptions(
            Enabled: configuration.GetValue("Capture:FrameDumpEnabled", false),
            DumpEveryNFrames: configuration.GetValue("Capture:FrameDumpEveryNFrames", 0),
            OutputDirectory: configuration.GetValue("Capture:FrameDumpOutputDirectory", "frame_dumps")
        );

        return new ScanSessionOptions(
            TargetFps: settings.TargetFps,
            OcrFrameDiffThreshold: settings.OcrFrameDiffThreshold,
            Preprocess: preprocessOptions,
            Ocr: ocrOptions,
            Extraction: new ExtractionOptions(),
            Stabilizer: stabilizerOptions,
            Overlay: new OverlayOptions(
                Theme: settings.Theme,
                ShowRoiOutline: settings.ShowRoiOutline),
            CaptureDump: captureDumpOptions
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
            new("calibration_border_top", string.Empty, 1f, new RectF(left, top, roi.Width, thickness), true),
            new("calibration_border_bottom", string.Empty, 1f, new RectF(left, bottom - thickness, roi.Width, thickness), true),
            new("calibration_border_left", string.Empty, 1f, new RectF(left, top, thickness, roi.Height), true),
            new("calibration_border_right", string.Empty, 1f, new RectF(right - thickness, top, thickness, roi.Height), true),
            new("calibration_inset_top", string.Empty, 1f, new RectF(innerLeft, innerTop, innerWidth, thickness), true),
            new("calibration_inset_bottom", string.Empty, 1f, new RectF(innerLeft, bottom - inset - thickness, innerWidth, thickness), true),
            new("calibration_inset_left", string.Empty, 1f, new RectF(innerLeft, innerTop, thickness, innerHeight), true),
            new("calibration_inset_right", string.Empty, 1f, new RectF(right - inset - thickness, innerTop, thickness, innerHeight), true),
            new("calibration_center_h", string.Empty, 1f, new RectF(innerLeft, centerY - thickness / 2f, innerWidth, thickness), true),
            new("calibration_center_v", string.Empty, 1f, new RectF(centerX - thickness / 2f, innerTop, thickness, innerHeight), true),
            new("calibration_third_h", string.Empty, 1f, new RectF(innerLeft, thirdY - thickness / 2f, innerWidth, thickness), true),
            new("calibration_two_third_h", string.Empty, 1f, new RectF(innerLeft, twoThirdY - thickness / 2f, innerWidth, thickness), true),
            new("calibration_third_v", string.Empty, 1f, new RectF(thirdX - thickness / 2f, innerTop, thickness, innerHeight), true),
            new("calibration_two_third_v", string.Empty, 1f, new RectF(twoThirdX - thickness / 2f, innerTop, thickness, innerHeight), true)
        };

        return highlights;
    }

    private static int PrintHelpAndReturnError()
    {
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("GUI Unfollowed (OCR) - CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  compute <following.csv> <followers.csv>   Compute NonFollowBack counts");
        Console.WriteLine("  scan --roi x,y,w,h                        Start scan loop (requires Windows capture/overlay)");
        Console.WriteLine("  scan-csv <following.csv> <followers.csv>  Start scan loop with CSV input (requires Windows capture/overlay)");
        Console.WriteLine("  convert-json <following.json> <followers.json> <output-dir>  Export CSVs from Instagram JSON");
        Console.WriteLine("  settings [--reset] [--settings path]      View or update stored settings");
        Console.WriteLine("  overlay-test [x y w h]                    Show click-through overlay and test alignment (Windows-only)");
        Console.WriteLine("  overlay-calibrate [x y w h]               Show ROI border + guides for calibration (Windows-only)");
        Console.WriteLine("  capture-test [x y w h] [count] [--preprocess]  Capture 1-3 ROI frames to BMP on disk (Windows-only)");
        Console.WriteLine("  ocr-test [x y w h]                        Run capture/preprocess/OCR once and print tokens (Windows-only)");
        Console.WriteLine();
        Console.WriteLine("Headless commands (cross-platform): compute, convert-json");
        Console.WriteLine("Windows-only commands (Win32 runtime required): scan, scan-csv, overlay-test, overlay-calibrate, capture-test, ocr-test");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --settings <path>           Path to settings JSON (overrides default AppData location)");
        Console.WriteLine("  --roi <x,y,w,h>             ROI rectangle, e.g. --roi 200,150,800,600");
        Console.WriteLine("  --target-fps <int>          Override target FPS for scans");
        Console.WriteLine("  --ocr-frame-diff <float>    Override OCR frame diff threshold");
        Console.WriteLine("  --ocr-min-confidence <float>  Override OCR min token confidence");
        Console.WriteLine("  --stabilizer-confidence <float>  Override stabilizer confidence threshold");
        Console.WriteLine("  --theme <Lime|Amber|Cyan>   Override overlay theme");
        Console.WriteLine("  --theme-mode <Light|Dark|System>  Override theme mode");
        Console.WriteLine("  --show-roi                Draw ROI outline during scan");
        Console.WriteLine("  --hide-roi                Hide ROI outline during scan");
        Console.WriteLine("  --count <int>               Frame count for capture-test (1-3)");
        Console.WriteLine("  --preprocess | --gray       Dump preprocessed frames for capture-test");
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

    private sealed class CliArguments
    {
        private readonly Dictionary<string, string?> _options;
        private readonly List<string> _positionals;

        private CliArguments(Dictionary<string, string?> options, List<string> positionals)
        {
            _options = options;
            _positionals = positionals;
        }

        public IReadOnlyList<string> Positionals => _positionals;

        public static CliArguments Parse(string[] args)
        {
            var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var positionals = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    positionals.Add(arg);
                    continue;
                }

                var trimmed = arg[2..];
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                var equalsIndex = trimmed.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    var key = trimmed[..equalsIndex];
                    var value = trimmed[(equalsIndex + 1)..];
                    options[key] = value;
                    continue;
                }

                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    options[trimmed] = args[i + 1];
                    i++;
                }
                else
                {
                    options[trimmed] = null;
                }
            }

            return new CliArguments(options, positionals);
        }

        public bool HasFlag(string key)
            => _options.TryGetValue(key, out var value) && string.IsNullOrEmpty(value);

        public bool HasOption(string key)
            => _options.ContainsKey(key);

        public string? GetOption(string key)
            => _options.TryGetValue(key, out var value) ? value : null;

        public bool TryGetOption(string key, out string value)
        {
            if (_options.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw))
            {
                value = raw;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}

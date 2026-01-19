using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Unfollowed.Capture;

namespace Unfollowed.App.Scan;

internal static class FrameDumpWriter
{
    public static void TryDumpFrame(CaptureFrame frame, CaptureDumpOptions options, long frameIndex, ILogger logger)
    {
        if (!options.Enabled)
            return;

        if (options.DumpEveryNFrames <= 0)
            return;

        if (frameIndex % options.DumpEveryNFrames != 0)
            return;

        try
        {
            var directory = string.IsNullOrWhiteSpace(options.OutputDirectory)
                ? Environment.CurrentDirectory
                : options.OutputDirectory;
            Directory.CreateDirectory(directory);

            var timestamp = frame.TimestampUtcTicks > 0
                ? new DateTime(frame.TimestampUtcTicks, DateTimeKind.Utc)
                : DateTime.UtcNow;
            var filename = string.Format(
                CultureInfo.InvariantCulture,
                "frame_{0:000000}_{1:yyyyMMdd_HHmmss_fff}.bmp",
                frameIndex,
                timestamp);
            var path = Path.Combine(directory, filename);
            SaveBgra32AsBmp(path, frame);

            logger.LogInformation("Dumped frame {FrameIndex} to {Path}", frameIndex, path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to dump frame {FrameIndex}", frameIndex);
        }
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
}

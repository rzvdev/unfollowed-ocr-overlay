using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Unfollowed.Core.Models;
using Unfollowed.Preprocess;

namespace Unfollowed.Ocr;

public interface IWindowsOcrEngineFactory
{
    OcrEngine? CreateEngine(OcrOptions options);
}

public sealed class WindowsOcrProvider : IOcrProvider
{
    private readonly IWindowsOcrEngineFactory _engineFactory;

    public WindowsOcrProvider(IWindowsOcrEngineFactory? engineFactory = null)
    {
        _engineFactory = engineFactory ?? new WindowsOcrEngineFactory();
    }

    public async Task<OcrResult> RecognizeAsync(ProcessedFrame frame, OcrOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (frame.Gray8.Length < frame.Width * frame.Height)
        {
            throw new InvalidOperationException("Processed frame buffer is smaller than expected.");
        }

        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Gray8, frame.Width, frame.Height, BitmapAlphaMode.Ignore);
        bitmap.CopyFromBuffer(frame.Gray8.AsBuffer());

        var engine = _engineFactory.CreateEngine(options);
        if (engine is null)
        {
            throw new InvalidOperationException("Windows OCR engine could not be created.");
        }

        var result = await engine.RecognizeAsync(bitmap).AsTask(ct);
        var whitelist = BuildWhitelist(options.CharacterWhitelist);
        var tokens = new List<OcrToken>();

        foreach (var word in result.Lines.SelectMany(line => line.Words))
        {
            ct.ThrowIfCancellationRequested();

            var text = word.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (whitelist is not null && !IsWhitelisted(text, whitelist))
            {
                continue;
            }

            var confidence = EstimateConfidence(text, options.AssumedTokenConfidence);
            if (confidence < options.MinTokenConfidence)
            {
                continue;
            }

            var rect = word.BoundingRect;
            var roiRect = new RectF((float)rect.X, (float)rect.Y, (float)rect.Width, (float)rect.Height);
            tokens.Add(new OcrToken(text, roiRect, confidence));
        }

        return new OcrResult(tokens, frame.Width, frame.Height);
    }

    private static float EstimateConfidence(string text, float assumedConfidence)
    {
        var confidence = Math.Clamp(assumedConfidence, 0.0f, 1.0f);

        if (text.Length <= 1)
        {
            confidence *= 0.4f;
        }
        else if (text.Length <= 2)
        {
            confidence *= 0.6f;
        }

        return confidence;
    }

    private static HashSet<char>? BuildWhitelist(string? whitelist)
    {
        if (string.IsNullOrWhiteSpace(whitelist))
        {
            return null;
        }

        return new HashSet<char>(whitelist);
    }

    private static bool IsWhitelisted(string text, HashSet<char> whitelist)
    {
        foreach (var character in text)
        {
            var lower = char.ToLowerInvariant(character);
            if (!whitelist.Contains(character) && !whitelist.Contains(lower))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class WindowsOcrEngineFactory : IWindowsOcrEngineFactory
{
    public OcrEngine? CreateEngine(OcrOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.LanguageTag))
        {
            try
            {
                var language = new Language(options.LanguageTag);
                return OcrEngine.TryCreateFromLanguage(language) ?? OcrEngine.TryCreateFromUserProfileLanguages();
            }
            catch (ArgumentException)
            {
                return OcrEngine.TryCreateFromUserProfileLanguages();
            }
        }

        return OcrEngine.TryCreateFromUserProfileLanguages();
    }
}

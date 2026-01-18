using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Normalization;
using Unfollowed.Core.Stabilization;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Scan;

public sealed class ScanSessionController : IScanSessionController
{
    private static readonly string[] FallbackNonFollowBack =
    {
        "unfollowed_demo",
        "sampleuser",
        "testaccount"
    };

    private readonly IOverlayRenderer _overlay;
    private readonly IFrameCapture _capture;
    private readonly IFramePreprocessor _preprocessor;
    private readonly IOcrProvider _ocr;
    private readonly IUsernameExtractor _extractor;
    private readonly IHighlightStabilizer _stabilizer;
    private readonly IUsernameNormalizer _normalizer;
    private readonly ILogger<ScanSessionController> _logger;
    private CancellationTokenSource? _sessionCts;
    private Task? _sessionTask;

    public ScanSessionController(
        IOverlayRenderer overlay,
        IFrameCapture capture,
        IFramePreprocessor preprocessor,
        IOcrProvider ocr,
        IUsernameExtractor extractor,
        IHighlightStabilizer stabilizer,
        IUsernameNormalizer normalizer,
        ILogger<ScanSessionController> logger)
    {
        _overlay = overlay;
        _capture = capture;
        _preprocessor = preprocessor;
        _ocr = ocr;
        _extractor = extractor;
        _stabilizer = stabilizer;
        _normalizer = normalizer;
        _logger = logger;
    }

    public async Task StartAsync(NonFollowBackData data, RoiSelection roi, ScanSessionOptions options, CancellationToken ct)
    {
        if (_sessionTask is { IsCompleted: false })
        {
            throw new InvalidOperationException("Scan session is already running.");
        }

        _stabilizer.Reset();

        await _capture.InitializeAsync(roi, ct);
        await _overlay.InitializeAsync(roi, options.Overlay, ct);

        var normalizedSet = BuildNormalizedSet(data);
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _sessionTask = RunLoopAsync(roi, options, normalizedSet, _sessionCts.Token);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _sessionCts?.Cancel();

        await _overlay.ClearAsync(ct);

        if (_sessionTask is not null)
        {
            try
            {
                await _sessionTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stabilizer.Reset();
        _sessionCts?.Dispose();
        _sessionCts = null;
        _sessionTask = null;

        await _overlay.ClearAsync(ct);
        await _overlay.DisposeAsync();
        await _capture.DisposeAsync();
    }

    private async Task RunLoopAsync(
        RoiSelection roi,
        ScanSessionOptions options,
        IReadOnlySet<string> nonFollowBackSet,
        CancellationToken ct)
    {
        var targetFps = Math.Max(1, options.TargetFps);
        var frameDuration = TimeSpan.FromSeconds(1.0 / targetFps);
        var frameIndex = 0L;

        while (!ct.IsCancellationRequested)
        {
            var frameStart = Stopwatch.GetTimestamp();

            try
            {
                var captureStart = Stopwatch.GetTimestamp();
                var frame = await _capture.CaptureAsync(ct);
                var captureElapsed = Stopwatch.GetElapsedTime(captureStart);

                var preprocessStart = Stopwatch.GetTimestamp();
                var processed = _preprocessor.Process(frame, options.Preprocess);
                var preprocessElapsed = Stopwatch.GetElapsedTime(preprocessStart);

                var ocrStart = Stopwatch.GetTimestamp();
                var ocrResult = await _ocr.RecognizeAsync(processed, options.Ocr, ct);
                var ocrElapsed = Stopwatch.GetElapsedTime(ocrStart);
                var tokens = ocrResult.Tokens
                    .Select(token => (token.Text, token.RoiRect, token.Confidence))
                    .ToArray();

                var extractStart = Stopwatch.GetTimestamp();
                var candidates = _extractor.ExtractCandidates(
                    tokens,
                    options.Extraction,
                    username => nonFollowBackSet.Contains(username),
                    raw => _normalizer.Normalize(raw));
                var extractElapsed = Stopwatch.GetElapsedTime(extractStart);

                var transform = new RoiToScreenTransform(
                    roi.X,
                    roi.Y,
                    roi.Width,
                    roi.Height,
                    processed.Width,
                    processed.Height);

                var highlights = _stabilizer.Stabilize(candidates, transform, options.Stabilizer);

                var renderStart = Stopwatch.GetTimestamp();
                await _overlay.RenderAsync(highlights, ct);
                var renderElapsed = Stopwatch.GetElapsedTime(renderStart);

                var totalElapsed = Stopwatch.GetElapsedTime(frameStart);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Frame {Frame} timings (ms): capture={CaptureMs:0.0} preprocess={PreprocessMs:0.0} ocr={OcrMs:0.0} extract={ExtractMs:0.0} render={RenderMs:0.0} total={TotalMs:0.0}",
                        frameIndex++,
                        captureElapsed.TotalMilliseconds,
                        preprocessElapsed.TotalMilliseconds,
                        ocrElapsed.TotalMilliseconds,
                        extractElapsed.TotalMilliseconds,
                        renderElapsed.TotalMilliseconds,
                        totalElapsed.TotalMilliseconds);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scan loop failed");
                break;
            }

            var elapsed = Stopwatch.GetElapsedTime(frameStart);
            var remaining = frameDuration - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(remaining, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private IReadOnlySet<string> BuildNormalizedSet(NonFollowBackData data)
    {
        var source = data.NonFollowBack.Count == 0
            ? FallbackNonFollowBack
            : data.NonFollowBack;

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in source)
        {
            var value = _normalizer.Normalize(raw);
            if (!string.IsNullOrWhiteSpace(value))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }
}

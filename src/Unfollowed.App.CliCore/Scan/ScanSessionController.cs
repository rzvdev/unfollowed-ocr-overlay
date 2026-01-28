using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Unfollowed.App.Services;
using Unfollowed.Capture;
using Unfollowed.Core.Models;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Normalization;
using Unfollowed.Core.Stabilization;
using Unfollowed.Ocr;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Scan;

/// <summary>
/// Orchestrates the live scan pipeline by initializing capture/overlay dependencies, running the
/// per-frame OCR and highlight stabilization loop, and coordinating cancellation/shutdown to ensure
/// overlays and capture resources are released cleanly between sessions.
/// </summary>
public sealed class ScanSessionController : IScanSessionController
{
    // Fallback list enables a demo pipeline run when no real non-follow-back data is provided.
    private static readonly string[] FallbackNonFollowBack =
    {
        "unfollowed_demo",
        "sampleuser",
        "testaccount"
    };

    private readonly IOverlayService _overlay;
    private readonly IFrameCapture _capture;
    private readonly IFramePreprocessor _preprocessor;
    private readonly IOcrProvider _ocr;
    private readonly IUsernameExtractor _extractor;
    private readonly IHighlightStabilizer _stabilizer;
    private readonly IUsernameNormalizer _normalizer;
    private readonly ILogger<ScanSessionController> _logger;
    private CancellationTokenSource? _sessionCts;
    private Task? _sessionTask;

    public Task? SessionTask => _sessionTask;

    public ScanSessionController(
        IOverlayService overlay,
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

    /// <summary>
    /// Starts a scan session by resetting the stabilizer, initializing capture and overlay dependencies,
    /// normalizing the non-follow-back set, and launching the scan loop with a linked cancellation token.
    /// </summary>
    /// <param name="data">The data set that supplies non-follow-back usernames.</param>
    /// <param name="roi">The region of interest for capture and overlay initialization.</param>
    /// <param name="options">Options that control preprocess, OCR, and overlay behavior.</param>
    /// <param name="ct">Cancellation token used for initialization and linked to the scan loop.</param>
    public async Task StartAsync(NonFollowBackData data, RoiSelection roi, ScanSessionOptions options, CancellationToken ct)
    {
        if (_sessionTask is { IsCompleted: false })
        {
            throw new InvalidOperationException("Scan session is already running.");
        }

        _stabilizer.Reset();

        await _capture.InitializeAsync(roi, ct);
        await _overlay.SetRoiAsync(roi, ct);
        await _overlay.InitializeAsync(options.Overlay, ct);
        await _overlay.UpdateHighlightsAsync(Array.Empty<Highlight>(), ct);

        var normalizedSet = BuildNormalizedSet(data);
        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _sessionTask = RunLoopAsync(roi, options, normalizedSet, _sessionCts.Token);
    }

    /// <summary>
    /// Signals cancellation, waits for the scan loop to exit, and guarantees cleanup by clearing overlays
    /// and disposing capture/overlay resources regardless of cancellation.
    /// </summary>
    /// <param name="ct">Cancellation token used for overlay cleanup.</param>
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

    /// <summary>
    /// Runs the per-frame scan loop, measuring timing for capture, preprocess, OCR, extraction,
    /// stabilization, and render stages while respecting the target FPS and logging frame timings.
    /// </summary>
    /// <param name="roi">The region of interest used to map OCR coordinates to the screen.</param>
    /// <param name="options">Options that control the pipeline stages.</param>
    /// <param name="nonFollowBackSet">Normalized set of usernames to match during extraction.</param>
    /// <param name="ct">Cancellation token that stops the loop and skips delay waits.</param>
    private async Task RunLoopAsync(
        RoiSelection roi,
        ScanSessionOptions options,
        IReadOnlySet<string> nonFollowBackSet,
        CancellationToken ct)
    {
        var targetFps = Math.Max(1, options.TargetFps);
        var frameDuration = TimeSpan.FromSeconds(1.0 / targetFps);
        var frameIndex = 0L;
        var ocrFrameIndex = 0L;
        var processedCount = 0L;
        var skippedCount = 0L;
        ProcessedFrame? previousProcessed = null;
        IReadOnlyList<Highlight> lastHighlights = Array.Empty<Highlight>();
        var lastSeenFrames = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var emptyHighlightStreak = 0;
        float? previousCandidateMeanY = null;
        var scrollCooldownRemaining = 0;

        while (!ct.IsCancellationRequested)
        {
            var frameStart = Stopwatch.GetTimestamp();

            try
            {
                var frameNumber = frameIndex;
                var captureStart = Stopwatch.GetTimestamp();
                var frame = await _capture.CaptureAsync(ct);
                var captureElapsed = Stopwatch.GetElapsedTime(captureStart);

                FrameDumpWriter.TryDumpFrame(frame, options.CaptureDump, frameNumber, _logger);

                var preprocessStart = Stopwatch.GetTimestamp();
                var processed = _preprocessor.Process(frame, options.Preprocess);
                var preprocessElapsed = Stopwatch.GetElapsedTime(preprocessStart);

                var diffRatio = previousProcessed is null
                    ? 1f
                    : CalculateFrameDifference(processed, previousProcessed);
                if (options.ScrollResetDiffThreshold > 0f && diffRatio >= options.ScrollResetDiffThreshold)
                {
                    _stabilizer.Reset();
                    lastHighlights = Array.Empty<Highlight>();
                    lastSeenFrames.Clear();
                    emptyHighlightStreak = 0;
                    previousCandidateMeanY = null;
                    if (options.ScrollCooldownFrames > 0)
                    {
                        scrollCooldownRemaining = Math.Max(scrollCooldownRemaining, options.ScrollCooldownFrames);
                    }
                    _logger.LogInformation(
                        "Scroll cooldown triggered by frame diff spike (diff={Diff:0.000}, threshold={Threshold:0.000}, cooldown={Cooldown} frames).",
                        diffRatio,
                        options.ScrollResetDiffThreshold,
                        options.ScrollCooldownFrames);
                    previousProcessed = processed;
                    continue;
                }
                var shouldRunOcr = options.OcrFrameDiffThreshold <= 0f
                    || previousProcessed is null
                    || diffRatio >= options.OcrFrameDiffThreshold;

                if (shouldRunOcr)
                {
                    processedCount++;
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation(
                            "Frame {Frame} OCR processed (diff={Diff:0.000}, threshold={Threshold:0.000}, processed={ProcessedCount}, skipped={SkippedCount}).",
                            frameIndex,
                            diffRatio,
                            options.OcrFrameDiffThreshold,
                            processedCount,
                            skippedCount);
                    }

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
                    var ocrFrameNumber = ocrFrameIndex++;
                    var candidateMeanY = candidates.Count == 0
                        ? (float?)null
                        : candidates.Average(candidate => candidate.RoiRect.Y + candidate.RoiRect.H * 0.5f);
                    var scrollDetected = false;
                    if (candidateMeanY.HasValue
                        && previousCandidateMeanY.HasValue
                        && options.ScrollResetOcrShiftRatio > 0f
                        && processed.Height > 0)
                    {
                        var delta = MathF.Abs(candidateMeanY.Value - previousCandidateMeanY.Value);
                        var deltaRatio = delta / processed.Height;
                        if (deltaRatio >= options.ScrollResetOcrShiftRatio)
                        {
                            _stabilizer.Reset();
                            lastHighlights = Array.Empty<Highlight>();
                            lastSeenFrames.Clear();
                            emptyHighlightStreak = 0;
                            previousCandidateMeanY = null;
                            if (options.ScrollCooldownFrames > 0)
                            {
                                scrollCooldownRemaining = Math.Max(scrollCooldownRemaining, options.ScrollCooldownFrames);
                            }
                            scrollDetected = true;
                            _logger.LogInformation(
                                "Scroll cooldown triggered by OCR Y-shift (delta={Delta:0.0}px, ratio={Ratio:0.000}, threshold={Threshold:0.000}, cooldown={Cooldown} frames).",
                                delta,
                                deltaRatio,
                                options.ScrollResetOcrShiftRatio,
                                options.ScrollCooldownFrames);
                        }
                    }

                    var transform = new RoiToScreenTransform(
                        roi.X,
                        roi.Y,
                        roi.Width,
                        roi.Height,
                        processed.Width,
                        processed.Height);

                    var hasFreshHighlights = false;
                    if (scrollDetected)
                    {
                        hasFreshHighlights = false;
                    }
                    else if (candidates.Count == 0)
                    {
                        _stabilizer.Reset();
                        emptyHighlightStreak = 0;
                        previousCandidateMeanY = null;
                    }
                    else
                    {
                        var stabilized = _stabilizer.Stabilize(candidates, transform, options.Stabilizer);
                        if (stabilized.Count > 0)
                        {
                            lastHighlights = stabilized;
                            hasFreshHighlights = true;
                        }
                        previousCandidateMeanY = candidateMeanY;
                    }

                    if (!scrollDetected)
                    {
                        if (candidates.Count > 0 && !hasFreshHighlights)
                        {
                            emptyHighlightStreak++;
                        }
                        else
                        {
                            emptyHighlightStreak = 0;
                        }

                        if (options.HighlightEmptyResetFrames > 0
                            && emptyHighlightStreak >= options.HighlightEmptyResetFrames)
                        {
                            lastHighlights = Array.Empty<Highlight>();
                            lastSeenFrames.Clear();
                            emptyHighlightStreak = 0;
                        }

                        if (hasFreshHighlights)
                        {
                            foreach (var highlight in lastHighlights)
                            {
                                lastSeenFrames[highlight.UsernameNormalized] = ocrFrameNumber;
                            }
                        }

                        if (options.HighlightTtlFrames > 0 && lastSeenFrames.Count > 0)
                        {
                            var expired = lastSeenFrames
                                .Where(pair => ocrFrameNumber - pair.Value > options.HighlightTtlFrames)
                                .Select(pair => pair.Key)
                                .ToArray();
                            if (expired.Length > 0)
                            {
                                foreach (var username in expired)
                                {
                                    lastSeenFrames.Remove(username);
                                }

                                if (lastHighlights.Count > 0)
                                {
                                    lastHighlights = lastHighlights
                                        .Where(highlight => lastSeenFrames.ContainsKey(highlight.UsernameNormalized))
                                        .ToArray();
                                }
                            }
                        }
                    }

                    if (scrollCooldownRemaining > 0)
                    {
                        scrollCooldownRemaining--;
                    }

                    TimeSpan renderElapsed;
                    if (scrollCooldownRemaining > 0)
                    {
                        renderElapsed = TimeSpan.Zero;
                    }
                    else
                    {
                        var renderStart = Stopwatch.GetTimestamp();
                        await _overlay.UpdateHighlightsAsync(lastHighlights, ct);
                        renderElapsed = Stopwatch.GetElapsedTime(renderStart);
                    }

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
                    else
                    {
                        _logger.LogInformation(
                            "Frame {Frame} timings (ms): capture={CaptureMs:0.0} preprocess={PreprocessMs:0.0} ocr={OcrMs:0.0} extract={ExtractMs:0.0} render={RenderMs:0.0} total={TotalMs:0.0}",
                            frameNumber,
                            captureElapsed.TotalMilliseconds,
                            preprocessElapsed.TotalMilliseconds,
                            ocrElapsed.TotalMilliseconds,
                            extractElapsed.TotalMilliseconds,
                            renderElapsed.TotalMilliseconds,
                            totalElapsed.TotalMilliseconds);
                    }
                    frameIndex++;
                }
                else
                {
                    skippedCount++;
                }

                previousProcessed = processed;
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
        // Keep a small fallback list so the pipeline can run even when the real dataset is empty.
        var source = data.NonFollowBack.Count == 0
            ? FallbackNonFollowBack
            : data.NonFollowBack;

        // Normalize whichever list is active so downstream matching uses the same normalization rules.
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

    private static float CalculateFrameDifference(ProcessedFrame current, ProcessedFrame previous)
    {
        if (current.Width != previous.Width || current.Height != previous.Height)
        {
            return 1f;
        }

        var currentBytes = current.Gray8;
        var previousBytes = previous.Gray8;
        if (currentBytes.Length != previousBytes.Length)
        {
            return 1f;
        }

        if (currentBytes.Length == 0)
        {
            return 0f;
        }

        long diffSum = 0;
        for (var i = 0; i < currentBytes.Length; i++)
        {
            diffSum += Math.Abs(currentBytes[i] - previousBytes[i]);
        }

        return diffSum / (currentBytes.Length * 255f);
    }
}

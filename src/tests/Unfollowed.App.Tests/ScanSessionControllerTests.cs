using Microsoft.Extensions.Logging.Abstractions;
using Unfollowed.App.Scan;
using Unfollowed.App.Services;
using Unfollowed.Capture;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Models;
using Unfollowed.Core.Normalization;
using Unfollowed.Core.Stabilization;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

namespace Unfollowed.App.Tests;

public sealed class ScanSessionControllerTests
{
    [Fact]
    public async Task Start_Twice_Without_Stopping_Throws()
    {
        var captureGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new ScanSessionController(
            new FakeOverlayService(),
            new BlockingFrameCapture(captureGate),
            new FakePreprocessor(),
            new FakeOcrProvider(),
            new CapturingExtractor(),
            new FakeStabilizer(),
            new PrefixNormalizer("norm:"),
            NullLogger<ScanSessionController>.Instance);

        var data = BuildData(Array.Empty<string>());
        var roi = new RoiSelection(0, 0, 100, 100);
        var options = BuildOptions();

        await controller.StartAsync(data, roi, options, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.StartAsync(data, roi, options, CancellationToken.None));

        captureGate.TrySetResult(true);
        await controller.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EmptyNonFollowBack_UsesFallbackNormalizedValuesInExtractor()
    {
        var renderSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var extractor = new CapturingExtractor();
        var controller = new ScanSessionController(
            new FakeOverlayService(() =>
            {
                renderSignal.TrySetResult(true);
                cts.Cancel();
            }),
            new SingleFrameCapture(),
            new FakePreprocessor(),
            new FakeOcrProvider(),
            extractor,
            new FakeStabilizer(),
            new PrefixNormalizer("norm:"),
            NullLogger<ScanSessionController>.Instance);

        var data = BuildData(Array.Empty<string>());
        var roi = new RoiSelection(0, 0, 100, 100);
        var options = BuildOptions();

        await controller.StartAsync(data, roi, options, cts.Token);

        await renderSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await controller.StopAsync(CancellationToken.None);

        Assert.NotNull(extractor.IsInNonFollowBackSet);

        var expected = new[]
        {
            "norm:unfollowed_demo",
            "norm:sampleuser",
            "norm:testaccount"
        };

        foreach (var username in expected)
        {
            Assert.True(extractor.IsInNonFollowBackSet!(username));
        }

        Assert.False(extractor.IsInNonFollowBackSet!("unfollowed_demo"));
    }

    private static NonFollowBackData BuildData(IReadOnlyCollection<string> nonFollowBack)
        => new(
            Following: Array.Empty<string>(),
            Followers: Array.Empty<string>(),
            NonFollowBack: nonFollowBack,
            FollowingStats: new CsvImportStats(0, 0, 0, 0),
            FollowersStats: new CsvImportStats(0, 0, 0, 0));

    private static ScanSessionOptions BuildOptions()
        => new(
            TargetFps: 4,
            OcrFrameDiffThreshold: 0.0f,
            0.0f,
            0.0f,
            Preprocess: new PreprocessOptions(),
            Ocr: new OcrOptions(),
            Extraction: new ExtractionOptions(),
            Stabilizer: new StabilizerOptions(),
            Overlay: new OverlayOptions(),
            CaptureDump: new CaptureDumpOptions());

    private sealed class BlockingFrameCapture : IFrameCapture
    {
        private readonly TaskCompletionSource<bool> _gate;

        public BlockingFrameCapture(TaskCompletionSource<bool> gate)
        {
            _gate = gate;
        }

        public Task InitializeAsync(RoiSelection roi, CancellationToken ct) => Task.CompletedTask;

        public async Task<CaptureFrame> CaptureAsync(CancellationToken ct)
        {
            await _gate.Task.WaitAsync(ct);
            return new CaptureFrame(new byte[4], 1, 1, DateTime.UtcNow.Ticks);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SingleFrameCapture : IFrameCapture
    {
        public Task InitializeAsync(RoiSelection roi, CancellationToken ct) => Task.CompletedTask;

        public Task<CaptureFrame> CaptureAsync(CancellationToken ct)
            => Task.FromResult(new CaptureFrame(new byte[4], 1, 1, DateTime.UtcNow.Ticks));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakePreprocessor : IFramePreprocessor
    {
        public ProcessedFrame Process(CaptureFrame frame, PreprocessOptions options)
            => new(new byte[1], frame.Width, frame.Height);
    }

    private sealed class FakeOcrProvider : IOcrProvider
    {
        public Task<OcrResult> RecognizeAsync(ProcessedFrame frame, OcrOptions options, CancellationToken ct)
            => Task.FromResult(new OcrResult(Array.Empty<OcrToken>(), frame.Width, frame.Height));
    }

    private sealed class FakeOverlayService : IOverlayService
    {
        private readonly Action? _onRender;

        public FakeOverlayService(Action? onRender = null)
        {
            _onRender = onRender;
        }

        public Task SetRoiAsync(RoiSelection roi, CancellationToken ct) => Task.CompletedTask;

        public Task InitializeAsync(OverlayOptions options, CancellationToken ct) => Task.CompletedTask;

        public Task UpdateHighlightsAsync(IReadOnlyList<Highlight> highlights, CancellationToken ct)
        {
            _onRender?.Invoke();
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStabilizer : IHighlightStabilizer
    {
        public IReadOnlyList<Highlight> Stabilize(
            IReadOnlyList<MatchCandidate> candidates,
            RoiToScreenTransform transform,
            StabilizerOptions options)
            => Array.Empty<Highlight>();

        public void Reset()
        {
        }
    }

    private sealed class CapturingExtractor : IUsernameExtractor
    {
        public Func<string, bool>? IsInNonFollowBackSet { get; private set; }

        public IReadOnlyList<MatchCandidate> ExtractCandidates(
            IReadOnlyCollection<(string Text, RectF RoiRect, float Confidence)> ocrTokens,
            ExtractionOptions options,
            Func<string, bool> isInNonFollowBackSet,
            Func<string, string> normalize)
        {
            IsInNonFollowBackSet = isInNonFollowBackSet;
            return Array.Empty<MatchCandidate>();
        }
    }

    private sealed class PrefixNormalizer : IUsernameNormalizer
    {
        private readonly string _prefix;

        public PrefixNormalizer(string prefix)
        {
            _prefix = prefix;
        }

        public string Normalize(string raw) => $"{_prefix}{raw}";
    }
}

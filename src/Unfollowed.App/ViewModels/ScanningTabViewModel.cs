using System.Collections.ObjectModel;
using System.Windows.Input;
using Unfollowed.App.Scan;
using Unfollowed.App.Services;
using Unfollowed.Capture;
using Unfollowed.Core.Extraction;
using Unfollowed.Core.Models;
using Unfollowed.Core.Stabilization;
using Unfollowed.Ocr;
using Unfollowed.Overlay;
using Unfollowed.Preprocess;

namespace Unfollowed.App.ViewModels;

public sealed class ScanningTabViewModel : ViewModelBase
{
    private const string RoiMissingStatus = "ROI not selected.";
    private const string RoiReadyStatus = "ROI ready.";
    private readonly DataTabViewModel _data;
    private readonly IScanSessionController _scanController;
    private readonly IOverlayService _overlayService;
    private readonly IRoiSelector _roiSelector;
    private readonly IFrameCapture _capture;
    private readonly IFramePreprocessor _preprocessor;
    private readonly IOcrProvider _ocr;
    private readonly RelayCommand _startCommand;
    private readonly RelayCommand _stopCommand;
    private readonly RelayCommand _testCaptureCommand;
    private readonly RelayCommand _testOcrCommand;
    private readonly RelayCommand _testOverlayCommand;
    private bool _isRunning;
    private bool _canStart;
    private bool _hasCsvData;
    private bool _hasRoi;
    private RoiSelection? _roi;
    private string _roiStatus = RoiMissingStatus;
    private string _testToolsStatus = "Test tools idle.";
    private double _fps = 15;
    private double _confidenceThreshold = 0.85;
    private string? _selectedProfile;
    private bool _showBadgeText = true;
    private bool _showOcrText = true;
    private bool _showRoiOutline;
    private double _ocrFrameDiffThreshold = 0.02;
    private bool _captureDumpEnabled;
    private int _captureDumpEveryNFrames;
    private string _captureDumpOutputDirectory = "frame_dumps";
    private bool _alwaysOnTop = true;
    private bool _clickThrough = true;
    private OverlayTheme _overlayTheme = OverlayTheme.Lime;
    private bool _isTestRunning;

    public ScanningTabViewModel(
        DataTabViewModel data,
        IScanSessionController scanController,
        IOverlayService overlayService,
        IRoiSelector roiSelector,
        IFrameCapture capture,
        IFramePreprocessor preprocessor,
        IOcrProvider ocr)
    {
        _data = data;
        _scanController = scanController;
        _overlayService = overlayService;
        _roiSelector = roiSelector;
        _capture = capture;
        _preprocessor = preprocessor;
        _ocr = ocr;

        SelectRoiCommand = new RelayCommand(_ => SelectRoi());
        _startCommand = new RelayCommand(_ => Start(), _ => CanStart);
        _stopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
        _testCaptureCommand = new RelayCommand(_ => TestCapture(), _ => CanRunTestTools());
        _testOcrCommand = new RelayCommand(_ => TestOcr(), _ => CanRunTestTools());
        _testOverlayCommand = new RelayCommand(_ => TestOverlay(), _ => CanRunTestTools());
        StartCommand = _startCommand;
        StopCommand = _stopCommand;
        TestCaptureCommand = _testCaptureCommand;
        TestOcrCommand = _testOcrCommand;
        TestOverlayCommand = _testOverlayCommand;
        Profiles = new ObservableCollection<string>
        {
            "Default",
            "High accuracy",
            "Fast scan"
        };
        SelectedProfile = Profiles[0];
        OverlayThemes = new ObservableCollection<OverlayTheme>(Enum.GetValues<OverlayTheme>());
        UpdateCanStart();
    }

    public ICommand SelectRoiCommand { get; }

    public ICommand StartCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand TestCaptureCommand { get; }

    public ICommand TestOcrCommand { get; }

    public ICommand TestOverlayCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                UpdateCanStart();
            }
        }
    }

    public bool IsTestRunning
    {
        get => _isTestRunning;
        private set
        {
            if (SetProperty(ref _isTestRunning, value))
            {
                RaiseTestToolsCanExecute();
            }
        }
    }

    public bool CanStart
    {
        get => _canStart;
        private set => SetProperty(ref _canStart, value);
    }

    public string RoiStatus
    {
        get => _roiStatus;
        private set => SetProperty(ref _roiStatus, value);
    }

    public string TestToolsStatus
    {
        get => _testToolsStatus;
        set => SetProperty(ref _testToolsStatus, value);
    }

    public double Fps
    {
        get => _fps;
        set => SetProperty(ref _fps, value);
    }

    public double ConfidenceThreshold
    {
        get => _confidenceThreshold;
        set => SetProperty(ref _confidenceThreshold, value);
    }

    public ObservableCollection<string> Profiles { get; }

    public ObservableCollection<OverlayTheme> OverlayThemes { get; }

    public string? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool ShowBadgeText
    {
        get => _showBadgeText;
        set => SetProperty(ref _showBadgeText, value);
    }

    public bool ShowOcrText
    {
        get => _showOcrText;
        set => SetProperty(ref _showOcrText, value);
    }

    public bool ShowRoiOutline
    {
        get => _showRoiOutline;
        set => SetProperty(ref _showRoiOutline, value);
    }

    public double OcrFrameDiffThreshold
    {
        get => _ocrFrameDiffThreshold;
        set => SetProperty(ref _ocrFrameDiffThreshold, value);
    }

    public bool CaptureDumpEnabled
    {
        get => _captureDumpEnabled;
        set => SetProperty(ref _captureDumpEnabled, value);
    }

    public int CaptureDumpEveryNFrames
    {
        get => _captureDumpEveryNFrames;
        set => SetProperty(ref _captureDumpEveryNFrames, value);
    }

    public string CaptureDumpOutputDirectory
    {
        get => _captureDumpOutputDirectory;
        set => SetProperty(ref _captureDumpOutputDirectory, value);
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => SetProperty(ref _alwaysOnTop, value);
    }

    public bool ClickThrough
    {
        get => _clickThrough;
        set => SetProperty(ref _clickThrough, value);
    }

    public OverlayTheme Theme
    {
        get => _overlayTheme;
        set => SetProperty(ref _overlayTheme, value);
    }

    public bool HasCsvData
    {
        get => _hasCsvData;
        set
        {
            if (SetProperty(ref _hasCsvData, value))
            {
                UpdateCanStart();
            }
        }
    }

    private void SelectRoi()
    {
        _ = SelectRoiAsync();
    }

    private async Task SelectRoiAsync()
    {
        try
        {
            var roi = await _roiSelector.SelectRegionAsync(CancellationToken.None);
            _roi = roi;
            _hasRoi = true;
            RoiStatus = RoiReadyStatus;
            await _overlayService.SetRoiAsync(roi, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            RoiStatus = "ROI selection canceled.";
        }
        catch (Exception ex)
        {
            _hasRoi = false;
            RoiStatus = $"ROI selection failed: {ex.Message}";
        }
        finally
        {
            UpdateCanStart();
        }
    }

    private void Start()
    {
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        if (_roi is null)
        {
            RoiStatus = RoiMissingStatus;
            return;
        }

        var data = _data.ComputedData;
        if (data is null)
        {
            RoiStatus = "Missing non-follow-back data.";
            return;
        }

        IsRunning = true;
        UpdateCanStart();

        try
        {
            var options = BuildOptions();
            await _scanController.StartAsync(data, _roi, options, CancellationToken.None);
            var sessionTask = _scanController.SessionTask;
            if (sessionTask is not null)
            {
                _ = sessionTask.ContinueWith(
                    task =>
                    {
                        if (task.IsCanceled)
                        {
                            RoiStatus = "Scan canceled.";
                        }
                        else if (task.Exception is not null)
                        {
                            RoiStatus = $"Scan failed: {task.Exception.GetBaseException().Message}";
                        }
                        else
                        {
                            RoiStatus = "Scan stopped.";
                        }

                        IsRunning = false;
                        UpdateCanStart();
                    },
                    TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
        catch (Exception ex)
        {
            IsRunning = false;
            RoiStatus = $"Scan failed: {ex.Message}";
            UpdateCanStart();
        }
    }

    private void Stop()
    {
        _ = StopAsync();
    }

    private async Task StopAsync()
    {
        try
        {
            await _scanController.StopAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            RoiStatus = $"Stop failed: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            UpdateCanStart();
        }
    }

    private void UpdateCanStart()
    {
        CanStart = !_isRunning && _hasRoi && _hasCsvData;
        _startCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
        RaiseTestToolsCanExecute();
    }

    private bool CanRunTestTools() => !IsRunning && !IsTestRunning;

    private void RaiseTestToolsCanExecute()
    {
        _testCaptureCommand.RaiseCanExecuteChanged();
        _testOcrCommand.RaiseCanExecuteChanged();
        _testOverlayCommand.RaiseCanExecuteChanged();
    }

    private void AppendTestStatus(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var line = $"{timestamp} {message}";
        TestToolsStatus = string.IsNullOrWhiteSpace(TestToolsStatus) || TestToolsStatus == "Test tools idle."
            ? line
            : $"{TestToolsStatus}{Environment.NewLine}{line}";
    }

    private void TestCapture()
    {
        _ = TestCaptureAsync();
    }

    private async Task TestCaptureAsync()
    {
        if (_roi is null)
        {
            AppendTestStatus("Capture test skipped: ROI not selected.");
            return;
        }

        IsTestRunning = true;
        AppendTestStatus("Capture test started.");

        try
        {
            await _capture.InitializeAsync(_roi, CancellationToken.None);
            var frame = await _capture.CaptureAsync(CancellationToken.None);
            AppendTestStatus($"Captured frame {frame.Width}x{frame.Height}.");
        }
        catch (Exception ex)
        {
            AppendTestStatus($"Capture test failed: {ex.Message}");
        }
        finally
        {
            await _capture.DisposeAsync();
            IsTestRunning = false;
        }
    }

    private void TestOcr()
    {
        _ = TestOcrAsync();
    }

    private async Task TestOcrAsync()
    {
        if (_roi is null)
        {
            AppendTestStatus("OCR test skipped: ROI not selected.");
            return;
        }

        IsTestRunning = true;
        AppendTestStatus("OCR test started.");

        try
        {
            var options = BuildOptions();
            await _capture.InitializeAsync(_roi, CancellationToken.None);
            var frame = await _capture.CaptureAsync(CancellationToken.None);
            var processed = _preprocessor.Process(frame, options.Preprocess);
            var result = await _ocr.RecognizeAsync(processed, options.Ocr, CancellationToken.None);

            AppendTestStatus($"OCR tokens: {result.Tokens.Count}.");

            var sampleTokens = result.Tokens
                .OrderByDescending(token => token.Confidence)
                .Take(5)
                .Select(token => $"{token.Text} ({token.Confidence:0.00})")
                .ToArray();

            if (sampleTokens.Length > 0)
            {
                AppendTestStatus($"Top tokens: {string.Join(", ", sampleTokens)}");
            }
            else
            {
                AppendTestStatus("No tokens returned from OCR.");
            }
        }
        catch (Exception ex)
        {
            AppendTestStatus($"OCR test failed: {ex.Message}");
        }
        finally
        {
            await _capture.DisposeAsync();
            IsTestRunning = false;
        }
    }

    private void TestOverlay()
    {
        _ = TestOverlayAsync();
    }

    private async Task TestOverlayAsync()
    {
        if (_roi is null)
        {
            AppendTestStatus("Overlay test skipped: ROI not selected.");
            return;
        }

        IsTestRunning = true;
        AppendTestStatus("Overlay test started.");

        try
        {
            var options = BuildOptions();
            await _overlayService.SetRoiAsync(_roi, CancellationToken.None);
            await _overlayService.InitializeAsync(options.Overlay, CancellationToken.None);

            var highlight = new Highlight(
                "test_overlay",
                "Test overlay",
                1f,
                new RectF(
                    _roi.X + _roi.Width * 0.1f,
                    _roi.Y + _roi.Height * 0.1f,
                    Math.Max(1f, _roi.Width * 0.3f),
                    Math.Max(1f, _roi.Height * 0.1f)),
                true);

            await _overlayService.UpdateHighlightsAsync(new[] { highlight }, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));
            await _overlayService.ClearAsync(CancellationToken.None);

            AppendTestStatus("Overlay test completed.");
        }
        catch (Exception ex)
        {
            AppendTestStatus($"Overlay test failed: {ex.Message}");
        }
        finally
        {
            await _overlayService.DisposeAsync();
            IsTestRunning = false;
        }
    }

    private ScanSessionOptions BuildOptions()
    {
        var confidence = (float)Math.Clamp(_confidenceThreshold, 0.0, 1.0);
        var fps = Math.Max(1, (int)Math.Round(_fps));
        var ocrFrameDiff = (float)Math.Clamp(_ocrFrameDiffThreshold, 0.0, 1.0);
        var dumpEveryNFrames = Math.Max(0, _captureDumpEveryNFrames);
        var outputDirectory = string.IsNullOrWhiteSpace(_captureDumpOutputDirectory)
            ? "frame_dumps"
            : _captureDumpOutputDirectory;

        return new ScanSessionOptions(
            TargetFps: fps,
            OcrFrameDiffThreshold: ocrFrameDiff,
            ScrollResetDiffThreshold: 0.15f,
            ScrollCooldownFrames: 2,
            ScrollResetOcrShiftRatio: 0.35f,
            HighlightTtlFrames: 3,
            HighlightEmptyResetFrames: 2,
            Preprocess: new PreprocessOptions(Profile: ResolveProfile()),
            Ocr: new OcrOptions(MinTokenConfidence: confidence),
            Extraction: new ExtractionOptions(MinTokenConfidence: confidence),
            Stabilizer: new StabilizerOptions(ConfidenceThreshold: confidence),
            Overlay: new OverlayOptions(
                AlwaysOnTop: _alwaysOnTop,
                ClickThrough: _clickThrough,
                ShowBadgeText: _showBadgeText,
                Theme: _overlayTheme,
                ShowOcrText: _showOcrText,
                ShowRoiOutline: _showRoiOutline),
            CaptureDump: new CaptureDumpOptions(
                Enabled: _captureDumpEnabled,
                DumpEveryNFrames: dumpEveryNFrames,
                OutputDirectory: outputDirectory));
    }

    private PreprocessProfile ResolveProfile()
        => SelectedProfile switch
        {
            "High accuracy" => PreprocessProfile.HighContrast,
            "Fast scan" => PreprocessProfile.LightUi,
            _ => PreprocessProfile.Default
        };

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

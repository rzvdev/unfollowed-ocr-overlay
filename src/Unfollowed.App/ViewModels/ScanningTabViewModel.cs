using System.Collections.ObjectModel;
using System.Windows.Input;
using Unfollowed.App.Scan;
using Unfollowed.App.Services;
using Unfollowed.Capture;
using Unfollowed.Core.Extraction;
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
    private readonly RelayCommand _startCommand;
    private readonly RelayCommand _stopCommand;
    private bool _isRunning;
    private bool _canStart;
    private bool _hasCsvData;
    private bool _hasRoi;
    private RoiSelection? _roi;
    private string _roiStatus = RoiMissingStatus;
    private double _fps = 15;
    private double _confidenceThreshold = 0.85;
    private string? _selectedProfile;
    private bool _showBadgeText = true;
    private bool _showOcrText = true;
    private double _ocrFrameDiffThreshold = 0.02;
    private bool _captureDumpEnabled;
    private int _captureDumpEveryNFrames;
    private string _captureDumpOutputDirectory = "frame_dumps";
    private bool _alwaysOnTop = true;
    private bool _clickThrough = true;
    private OverlayTheme _overlayTheme = OverlayTheme.Lime;

    public ScanningTabViewModel(
        DataTabViewModel data,
        IScanSessionController scanController,
        IOverlayService overlayService,
        IRoiSelector roiSelector)
    {
        _data = data;
        _scanController = scanController;
        _overlayService = overlayService;
        _roiSelector = roiSelector;

        SelectRoiCommand = new RelayCommand(_ => SelectRoi());
        _startCommand = new RelayCommand(_ => Start(), _ => CanStart);
        _stopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
        StartCommand = _startCommand;
        StopCommand = _stopCommand;
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
            Preprocess: new PreprocessOptions(Profile: ResolveProfile()),
            Ocr: new OcrOptions(MinTokenConfidence: confidence),
            Extraction: new ExtractionOptions(MinTokenConfidence: confidence),
            Stabilizer: new StabilizerOptions(ConfidenceThreshold: confidence),
            Overlay: new OverlayOptions(
                AlwaysOnTop: _alwaysOnTop,
                ClickThrough: _clickThrough,
                ShowBadgeText: _showBadgeText,
                Theme: _overlayTheme,
                ShowOcrText: _showOcrText),
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

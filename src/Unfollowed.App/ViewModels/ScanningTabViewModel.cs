using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Unfollowed.App.ViewModels;

public sealed class ScanningTabViewModel : ViewModelBase
{
    private const string RoiMissingStatus = "ROI not selected.";
    private const string RoiReadyStatus = "ROI ready.";
    private readonly RelayCommand _startCommand;
    private readonly RelayCommand _stopCommand;
    private bool _isRunning;
    private bool _canStart;
    private bool _hasCsvData;
    private bool _hasRoi;
    private string _roiStatus = RoiMissingStatus;
    private double _fps = 15;
    private double _confidenceThreshold = 0.85;
    private string? _selectedProfile;
    private bool _showOcrBoxes = true;
    private bool _showOcrText = true;

    public ScanningTabViewModel()
    {
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

    public string? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool ShowOcrBoxes
    {
        get => _showOcrBoxes;
        set => SetProperty(ref _showOcrBoxes, value);
    }

    public bool ShowOcrText
    {
        get => _showOcrText;
        set => SetProperty(ref _showOcrText, value);
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
        _hasRoi = true;
        RoiStatus = RoiReadyStatus;
        UpdateCanStart();
    }

    private void Start()
    {
        IsRunning = true;
        UpdateCanStart();
    }

    private void Stop()
    {
        IsRunning = false;
        UpdateCanStart();
    }

    private void UpdateCanStart()
    {
        CanStart = !_isRunning && _hasRoi && _hasCsvData;
        _startCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
    }

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

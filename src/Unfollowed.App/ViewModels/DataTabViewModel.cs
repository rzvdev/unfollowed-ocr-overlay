using System.Windows.Input;
using Microsoft.Win32;
using Unfollowed.Csv;

namespace Unfollowed.App.ViewModels;

public sealed class DataTabViewModel : ViewModelBase
{
    private readonly ICsvImporter _importer;
    private readonly INonFollowBackCalculator _calculator;
    private string? _followingPath;
    private string? _followersPath;
    private int _followingCount;
    private int _followersCount;
    private int _nonFollowBackCount;
    private string _statusMessage = "Select your following and followers CSV exports to compute the results.";
    private bool _hasError;
    private string _errorMessage = string.Empty;

    public DataTabViewModel(ICsvImporter importer, INonFollowBackCalculator calculator)
    {
        _importer = importer;
        _calculator = calculator;

        LoadFollowingCsvCommand = new RelayCommand(_ => LoadFollowingCsv());
        LoadFollowersCsvCommand = new RelayCommand(_ => LoadFollowersCsv());
    }

    public ICommand LoadFollowingCsvCommand { get; }

    public ICommand LoadFollowersCsvCommand { get; }

    public int FollowingCount
    {
        get => _followingCount;
        private set => SetProperty(ref _followingCount, value);
    }

    public int FollowersCount
    {
        get => _followersCount;
        private set => SetProperty(ref _followersCount, value);
    }

    public int NonFollowBackCount
    {
        get => _nonFollowBackCount;
        private set => SetProperty(ref _nonFollowBackCount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    private void LoadFollowingCsv()
    {
        var path = PromptForCsvPath("Select following.csv");
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Following CSV selection canceled.";
            return;
        }

        _followingPath = path;
        StatusMessage = $"Loaded following CSV: {System.IO.Path.GetFileName(path)}";
        ClearError();
        TryComputeResults();
    }

    private void LoadFollowersCsv()
    {
        var path = PromptForCsvPath("Select followers.csv");
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Followers CSV selection canceled.";
            return;
        }

        _followersPath = path;
        StatusMessage = $"Loaded followers CSV: {System.IO.Path.GetFileName(path)}";
        ClearError();
        TryComputeResults();
    }

    private void TryComputeResults()
    {
        if (string.IsNullOrWhiteSpace(_followingPath) || string.IsNullOrWhiteSpace(_followersPath))
        {
            return;
        }

        try
        {
            var following = _importer.ImportUsernames(_followingPath, new CsvImportOptions(), CancellationToken.None);
            var followers = _importer.ImportUsernames(_followersPath, new CsvImportOptions(), CancellationToken.None);
            var data = _calculator.Compute(following, followers);

            FollowingCount = data.Following.Count;
            FollowersCount = data.Followers.Count;
            NonFollowBackCount = data.NonFollowBack.Count;
            StatusMessage = "Computed non-follow-back results.";
            ClearError();
        }
        catch (Exception ex)
        {
            FollowingCount = 0;
            FollowersCount = 0;
            NonFollowBackCount = 0;
            HasError = true;
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to compute results.";
        }
    }

    private void ClearError()
    {
        if (HasError)
        {
            HasError = false;
        }

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ErrorMessage = string.Empty;
        }
    }

    private static string? PromptForCsvPath(string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = title,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
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

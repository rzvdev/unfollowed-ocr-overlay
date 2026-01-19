using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Forms;
using Microsoft.Win32;
using Unfollowed.Csv;
using Unfollowed.Core.Models;
using System.IO;

namespace Unfollowed.App.ViewModels;

public sealed class DataTabViewModel : ViewModelBase
{
    private readonly ICsvImporter _importer;
    private readonly INonFollowBackCalculator _calculator;
    private string? _followingPath;
    private string? _followersPath;
    private string? _followingJsonPath;
    private string? _followersJsonPath;
    private string? _outputDirectory;
    private int _followingCount;
    private int _followersCount;
    private int _nonFollowBackCount;
    private bool _hasCsvData;
    private NonFollowBackData? _computedData;
    private string _followingJsonLabel = "No following.json selected.";
    private string _followersJsonLabel = "No followers.json selected.";
    private string _outputDirectoryLabel = "No output folder selected.";
    private string _statusMessage = "Select your following and followers CSV exports to compute the results.";
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private readonly ObservableCollection<string> _following = new();
    private readonly ObservableCollection<string> _followers = new();
    private readonly ObservableCollection<string> _nonFollowBack = new();

    public DataTabViewModel(ICsvImporter importer, INonFollowBackCalculator calculator)
    {
        _importer = importer;
        _calculator = calculator;

        Following = new ReadOnlyObservableCollection<string>(_following);
        Followers = new ReadOnlyObservableCollection<string>(_followers);
        NonFollowBack = new ReadOnlyObservableCollection<string>(_nonFollowBack);

        LoadFollowingCsvCommand = new RelayCommand(_ => LoadFollowingCsv());
        LoadFollowersCsvCommand = new RelayCommand(_ => LoadFollowersCsv());
        SelectFollowingJsonCommand = new RelayCommand(_ => SelectFollowingJson());
        SelectFollowersJsonCommand = new RelayCommand(_ => SelectFollowersJson());
        SelectOutputDirectoryCommand = new RelayCommand(_ => SelectOutputDirectory());
        ConvertJsonToCsvCommand = new RelayCommand(_ => ConvertJsonToCsv());
    }

    public ICommand LoadFollowingCsvCommand { get; }

    public ICommand LoadFollowersCsvCommand { get; }

    public ICommand SelectFollowingJsonCommand { get; }

    public ICommand SelectFollowersJsonCommand { get; }

    public ICommand SelectOutputDirectoryCommand { get; }

    public ICommand ConvertJsonToCsvCommand { get; }

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

    public bool HasCsvData
    {
        get => _hasCsvData;
        private set => SetProperty(ref _hasCsvData, value);
    }

    public NonFollowBackData? ComputedData
    {
        get => _computedData;
        private set => SetProperty(ref _computedData, value);
    }

    public ReadOnlyObservableCollection<string> Following { get; }

    public ReadOnlyObservableCollection<string> Followers { get; }

    public ReadOnlyObservableCollection<string> NonFollowBack { get; }

    public string FollowingJsonLabel
    {
        get => _followingJsonLabel;
        private set => SetProperty(ref _followingJsonLabel, value);
    }

    public string FollowersJsonLabel
    {
        get => _followersJsonLabel;
        private set => SetProperty(ref _followersJsonLabel, value);
    }

    public string OutputDirectoryLabel
    {
        get => _outputDirectoryLabel;
        private set => SetProperty(ref _outputDirectoryLabel, value);
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

    private void SelectFollowingJson()
    {
        var path = PromptForJsonPath("Select following.json");
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Following JSON selection canceled.";
            return;
        }

        _followingJsonPath = path;
        FollowingJsonLabel = System.IO.Path.GetFileName(path);
        StatusMessage = $"Loaded following JSON: {System.IO.Path.GetFileName(path)}";
        ClearError();
    }

    private void SelectFollowersJson()
    {
        var path = PromptForJsonPath("Select followers.json");
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Followers JSON selection canceled.";
            return;
        }

        _followersJsonPath = path;
        FollowersJsonLabel = System.IO.Path.GetFileName(path);
        StatusMessage = $"Loaded followers JSON: {System.IO.Path.GetFileName(path)}";
        ClearError();
    }

    private void SelectOutputDirectory()
    {
        var path = PromptForOutputDirectory();
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Output folder selection canceled.";
            return;
        }

        _outputDirectory = path;
        OutputDirectoryLabel = path;
        StatusMessage = "Selected output folder.";
        ClearError();
    }

    private void ConvertJsonToCsv()
    {
        if (string.IsNullOrWhiteSpace(_followingJsonPath)
            || string.IsNullOrWhiteSpace(_followersJsonPath)
            || string.IsNullOrWhiteSpace(_outputDirectory))
        {
            HasError = true;
            ErrorMessage = "Select following.json, followers.json, and an output folder before converting.";
            StatusMessage = "Missing JSON conversion inputs.";
            return;
        }

        try
        {
            var exporter = new InstagramJsonCsvExporter();
            exporter.Export(_followingJsonPath, _followersJsonPath, _outputDirectory, CancellationToken.None);

            var followingCsvPath = System.IO.Path.Combine(_outputDirectory, "following.csv");
            var followersCsvPath = System.IO.Path.Combine(_outputDirectory, "followers.csv");
            var nonFollowBackCsvPath = System.IO.Path.Combine(_outputDirectory, "non_follow_back.csv");

            var following = _importer.ImportUsernames(followingCsvPath, new CsvImportOptions(), CancellationToken.None);
            var followers = _importer.ImportUsernames(followersCsvPath, new CsvImportOptions(), CancellationToken.None);
            var data = _calculator.Compute(following, followers);

            WriteUsernameCsv(nonFollowBackCsvPath, data.NonFollowBack, CancellationToken.None);

            StatusMessage = $"Converted JSON to CSV in {_outputDirectory} and created {System.IO.Path.GetFileName(nonFollowBackCsvPath)}.";
            StatusMessage = $"Converted JSON to CSV in {_outputDirectory}.";
            ClearError();

            var result = System.Windows.MessageBox.Show(
                "Conversion complete. Load the generated CSV files now?",
                "Load generated CSV files",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _followingPath = followingCsvPath;
                _followersPath = followersCsvPath;
                _followingPath = System.IO.Path.Combine(_outputDirectory, "following.csv");
                TryComputeResults();
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to convert JSON to CSV.";
        }
    }

    private void TryComputeResults()
    {
        if (string.IsNullOrWhiteSpace(_followingPath) || string.IsNullOrWhiteSpace(_followersPath))
        {
            HasCsvData = false;
            ClearCollections();
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
            HasCsvData = true;
            ComputedData = data;
            ResetCollection(_following, data.Following);
            ResetCollection(_followers, data.Followers);
            ResetCollection(_nonFollowBack, data.NonFollowBack);
            StatusMessage = "Computed non-follow-back results.";
            ClearError();
        }
        catch (Exception ex)
        {
            FollowingCount = 0;
            FollowersCount = 0;
            NonFollowBackCount = 0;
            HasCsvData = false;
            ComputedData = null;
            ClearCollections();
            HasError = true;
            ErrorMessage = ex.Message;
            StatusMessage = "Failed to compute results.";
        }
    }

    private void ClearCollections()
    {
        _following.Clear();
        _followers.Clear();
        _nonFollowBack.Clear();
    }

    private static void ResetCollection(ObservableCollection<string> target, IReadOnlyCollection<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
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
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = title,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PromptForJsonPath(string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = title,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? PromptForOutputDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select output folder",
            ShowNewFolderButton = true
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static void WriteUsernameCsv(string path, IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        using var writer = new StreamWriter(path, false);
        writer.WriteLine("username");

        foreach (var username in usernames)
        {
            ct.ThrowIfCancellationRequested();
            writer.WriteLine(username);
        }
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

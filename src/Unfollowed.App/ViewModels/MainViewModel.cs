namespace Unfollowed.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private int _selectedTabIndex;

    public MainViewModel(
        DataTabViewModel data,
        ScanningTabViewModel scanning,
        DiagnosticsTabViewModel diagnostics)
    {
        Data = data;
        Scanning = scanning;
        Diagnostics = diagnostics;
    }

    public DataTabViewModel Data { get; }

    public ScanningTabViewModel Scanning { get; }

    public DiagnosticsTabViewModel Diagnostics { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }
}

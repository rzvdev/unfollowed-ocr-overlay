using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using Unfollowed.App.Diagnostics;

namespace Unfollowed.App.ViewModels;

public sealed class DiagnosticsTabViewModel : ViewModelBase
{
    private const int MaxEntries = 500;
    private readonly ObservableCollection<LogEntry> _logEntries = new();
    private readonly SynchronizationContext? _context;
    private double _frameTimeTotal;
    private int _frameTimeCount;
    private double _ocrLatencyTotal;
    private int _ocrLatencyCount;
    private double _frameTimeAverage;
    private double _ocrLatencyAverage;

    public DiagnosticsTabViewModel(InAppLogSink logSink)
    {
        _context = SynchronizationContext.Current;
        LogEntries = new ReadOnlyObservableCollection<LogEntry>(_logEntries);
        ClearLogsCommand = new RelayCommand(_ => ClearLogs());

        logSink.LogReceived += (_, args) =>
        {
            if (_context is not null)
            {
                _context.Post(_ => AddEntry(args.Entry), null);
            }
            else
            {
                AddEntry(args.Entry);
            }
        };
    }

    public ReadOnlyObservableCollection<LogEntry> LogEntries { get; }

    public double FrameTimeAverage
    {
        get => _frameTimeAverage;
        private set => SetProperty(ref _frameTimeAverage, value);
    }

    public double OcrLatencyAverage
    {
        get => _ocrLatencyAverage;
        private set => SetProperty(ref _ocrLatencyAverage, value);
    }

    public ICommand ClearLogsCommand { get; }

    private void AddEntry(InAppLogEntry entry)
    {
        var message = entry.Exception is null
            ? entry.Message
            : $"{entry.Message} ({entry.Exception.Message})";
        var logEntry = new LogEntry(entry.Timestamp, entry.Level.ToString(), message, entry.Category);
        _logEntries.Add(logEntry);
        TrimEntries();
        UpdateAverages(entry.Properties);
    }

    private void TrimEntries()
    {
        while (_logEntries.Count > MaxEntries)
        {
            _logEntries.RemoveAt(0);
        }
    }

    private void UpdateAverages(IReadOnlyDictionary<string, object?>? properties)
    {
        if (properties is null)
        {
            return;
        }

        if (TryGetDouble(properties, "TotalMs", out var frameMs))
        {
            _frameTimeTotal += frameMs;
            _frameTimeCount++;
            FrameTimeAverage = _frameTimeTotal / _frameTimeCount;
        }

        if (TryGetDouble(properties, "OcrMs", out var ocrMs))
        {
            _ocrLatencyTotal += ocrMs;
            _ocrLatencyCount++;
            OcrLatencyAverage = _ocrLatencyTotal / _ocrLatencyCount;
        }
    }

    private static bool TryGetDouble(IReadOnlyDictionary<string, object?> properties, string key, out double value)
    {
        if (!properties.TryGetValue(key, out var raw) || raw is null)
        {
            value = 0d;
            return false;
        }

        switch (raw)
        {
            case double doubleValue:
                value = doubleValue;
                return true;
            case float floatValue:
                value = floatValue;
                return true;
            case decimal decimalValue:
                value = (double)decimalValue;
                return true;
            default:
                return double.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    private void ClearLogs()
    {
        _logEntries.Clear();
        _frameTimeTotal = 0d;
        _frameTimeCount = 0;
        _ocrLatencyTotal = 0d;
        _ocrLatencyCount = 0;
        FrameTimeAverage = 0d;
        OcrLatencyAverage = 0d;
    }

    public sealed record LogEntry(DateTimeOffset Timestamp, string Level, string Message, string Category);

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

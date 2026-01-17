using System.Windows.Threading;

namespace Unfollowed.Overlay.Win32.Infrastructure;

public sealed class WpfUiThreadHost : IDisposable
{
    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private readonly ManualResetEventSlim _ready = new(false);

    public WpfUiThreadHost()
    {
        _thread = new Thread(ThreadStart)
        {
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    public Dispatcher Dispatcher
       => _dispatcher ?? throw new InvalidOperationException("Dispatcher not initialized.");

    private void ThreadStart()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _ready.Set();
        Dispatcher.Run();
    }

    public Task InvokeAsync(Action action)
        => Dispatcher.InvokeAsync(action).Task;

    public Task<T> InvokeAsync<T>(Func<T> func)
        => Dispatcher.InvokeAsync(func).Task;

    public void Dispose()
    {
        if (_dispatcher is null) return;
        _dispatcher.InvokeShutdown();
        _thread.Join();
        _ready.Dispose();
    }
}

# Dispose checklist

Before declaring a class is `IDisposable`, verify:

## Minimum (sealed class, no finalizer)

```cs
public sealed class MyService : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 1. Unsubscribe from external events
        // 2. Dispose owned IDisposable fields
        // 3. Stop timers
        // 4. Cancel CancellationTokenSource
    }
}
```

## With finalizer (unmanaged resource — rare in this codebase)

```cs
public class MyHandle : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    ~MyHandle() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);  // <-- required, flagged by leak-guard if missing
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) { /* managed cleanup */ }
        if (_handle != IntPtr.Zero) { /* unmanaged free */ }
        _disposed = true;
    }
}
```

## WPF UserControl / Window owning resources

```cs
public partial class MyControl : UserControl
{
    private readonly DispatcherTimer _timer = new();

    public MyControl()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
        _timer.Tick += OnTick;
    }

    private void OnUnloaded(object s, RoutedEventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        Loaded   -= OnLoaded;
        Unloaded -= OnUnloaded;
    }
}
```

The `Unloaded` handler is the WPF equivalent of `Dispose` for visuals.
`Unloaded` may fire multiple times — make handlers idempotent.

## Long-lived host subscriptions

If a control subscribes to `Application.Current.MainWindow.Activated`, it
**must** unsubscribe on `Unloaded` or use `WeakEventManager.AddHandler`. Else
the host keeps the control alive forever.

## Common missed cases

| Pattern                              | What to clean up                          |
|--------------------------------------|-------------------------------------------|
| `FileSystemWatcher`                  | `Dispose()` + unsubscribe `Changed/Created/Deleted/Renamed` |
| `BackgroundWorker`                   | `CancelAsync()` + unsubscribe events       |
| `CancellationTokenSource`            | `Cancel()` + `Dispose()`                  |
| `MemoryMappedFile` / `ViewAccessor`  | `Dispose()` both                          |
| `HttpClient` (per-request)           | usually static; per-request needs `using` |
| WPF `RenderTargetBitmap`             | not IDisposable but freezing helps GC     |

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace MultiTenantHttpClientFactory.HotReload;

/// <summary>
/// An IChangeToken implementation that polls for changes on a configurable interval.
/// Useful for stores that don't support push notifications (database, external API, etc).
/// </summary>
public class PollingChangeToken : IChangeToken, IDisposable
{
    private readonly Func<CancellationToken, Task<bool>> _hasChangedAsync;
    private readonly ILogger? _logger;
    private readonly int _pollIntervalMs;
    private Timer? _pollTimer;
    private bool _hasChanged;
    private bool _disposed;
    private CancellationTokenSource? _cancellationTokenSource;
    private Action<object?>? _changeCallback;

    /// <summary>
    /// Creates a new polling change token.
    /// </summary>
    /// <param name="hasChangedAsync">Async delegate that returns true when a change is detected</param>
    /// <param name="pollIntervalMs">How often to poll, in milliseconds (default: 30,000 = 30 seconds)</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public PollingChangeToken(
        Func<CancellationToken, Task<bool>> hasChangedAsync,
        int pollIntervalMs = 30000,
        ILogger? logger = null)
    {
        if (hasChangedAsync == null)
            throw new ArgumentNullException(nameof(hasChangedAsync));

        if (pollIntervalMs <= 0)
            throw new ArgumentException("Poll interval must be positive", nameof(pollIntervalMs));

        _hasChangedAsync = hasChangedAsync;
        _pollIntervalMs = pollIntervalMs;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();
        _hasChanged = false;

        // Start polling
        StartPolling();
    }

    public bool HasChanged => _hasChanged;

    public bool ActiveChangeCallbacks => _changeCallback != null;

    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        _changeCallback = callback;

        // Return a disposable that unregisters the callback
        return new ChangeTokenRegistration(() => _changeCallback = null);
    }

    private void StartPolling()
    {
        if (_disposed || _cancellationTokenSource == null)
            return;

        _pollTimer = new Timer(
            async _ => await PollAsync(),
            null,
            TimeSpan.FromMilliseconds(_pollIntervalMs),
            TimeSpan.FromMilliseconds(_pollIntervalMs));
    }

    private async Task PollAsync()
    {
        try
        {
            if (_disposed || _cancellationTokenSource?.IsCancellationRequested == true)
                return;

            var changed = await _hasChangedAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

            if (changed && !_hasChanged)
            {
                _hasChanged = true;
                _logger?.LogInformation("Polling change token detected a change");

                // Stop polling
                _pollTimer?.Dispose();
                _pollTimer = null;

                // Fire the callback
                _changeCallback?.Invoke(null);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if disposed
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during change polling");
        }
    }

    private class ChangeTokenRegistration : IDisposable
    {
        private readonly Action _unregister;

        public ChangeTokenRegistration(Action unregister)
        {
            _unregister = unregister ?? throw new ArgumentNullException(nameof(unregister));
        }

        public void Dispose() => _unregister();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pollTimer?.Dispose();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}

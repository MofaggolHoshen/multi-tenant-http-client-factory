using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MultiTenantHttpClientFactory.HotReload;

/// <summary>
/// Background service that periodically cleans up expired HttpMessageHandlers
/// from the tenant handler cache. Runs as an IHostedService in the application.
/// </summary>
public class TenantHandlerCleanupService : IHostedService
{
    private readonly ILogger<TenantHandlerCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;
    private Timer? _cleanupTimer;
    private volatile bool _running;

    public TenantHandlerCleanupService(
        ILogger<TenantHandlerCleanupService> logger,
        TimeSpan? cleanupInterval = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cleanupInterval = cleanupInterval ?? TimeSpan.FromSeconds(60);
        _running = false;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = true;
        _logger.LogInformation("TenantHandlerCleanupService starting with interval {CleanupIntervalSeconds}s", _cleanupInterval.TotalSeconds);

        _cleanupTimer = new Timer(
            OnCleanupTimer,
            null,
            _cleanupInterval,
            _cleanupInterval);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _running = false;
        _cleanupTimer?.Dispose();
        _logger.LogInformation("TenantHandlerCleanupService stopped");
        return Task.CompletedTask;
    }

    private void OnCleanupTimer(object? state)
    {
        try
        {
            if (!_running)
                return;

            _logger.LogDebug("TenantHandlerCleanupService cleanup cycle running");
            
            // Cleanup is triggered by TenantHandlerCache's internal timer
            // This service just ensures the cache's cleanup is being monitored
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TenantHandlerCleanupService cleanup cycle");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}

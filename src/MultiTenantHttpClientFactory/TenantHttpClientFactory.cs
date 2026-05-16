using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory;

/// <summary>
/// Main implementation of ITenantHttpClientFactory.
/// Creates tenant-specific HttpClient instances with appropriate configuration.
/// </summary>
internal class TenantHttpClientFactory : ITenantHttpClientFactory
{
    private readonly ITenantConfigurationProvider _configProvider;
    private readonly TenantHandlerCache _handlerCache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantHttpClientFactory> _logger;

    public TenantHttpClientFactory(
        ITenantConfigurationProvider configProvider,
        TenantHandlerCache handlerCache,
        ITenantContext tenantContext,
        ILogger<TenantHttpClientFactory> logger)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _handlerCache = handlerCache ?? throw new ArgumentNullException(nameof(handlerCache));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an HttpClient configured for the specified tenant and optional endpoint.
    /// Explicit path - works in background jobs, event handlers, etc.
    /// </summary>
    public HttpClient CreateClient(string tenantId, string? endpointName = null)
    {
        if (string.IsNullOrEmpty(tenantId))
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));

        var config = _configProvider.GetConfigurationAsync(tenantId).GetAwaiter().GetResult()
            ?? throw new TenantNotFoundException(tenantId);

        _logger.LogDebug("Creating HttpClient for tenant {TenantId}, endpoint {EndpointName}", tenantId, endpointName ?? "default");
        return CreateClientInternal(config, endpointName);
    }

    /// <summary>
    /// Creates an HttpClient for the current tenant resolved from the HTTP context.
    /// Implicit path - uses scoped ITenantContext.
    /// </summary>
    public HttpClient CreateClient(string? endpointName = null)
    {
        if (!_tenantContext.IsResolved || string.IsNullOrEmpty(_tenantContext.TenantId))
            throw new TenantNotResolvedException();

        var config = _tenantContext.Configuration
            ?? throw new TenantNotResolvedException($"Tenant configuration not available for {_tenantContext.TenantId}");

        _logger.LogDebug("Creating HttpClient for context tenant {TenantId}, endpoint {EndpointName}", _tenantContext.TenantId, endpointName ?? "default");
        return CreateClientInternal(config, endpointName);
    }

    private HttpClient CreateClientInternal(TenantConfiguration config, string? endpointName)
    {
        // Determine which endpoint configuration to use
        EndpointConfiguration? endpointConfig = null;

        if (!string.IsNullOrEmpty(endpointName) && config.Endpoints.TryGetValue(endpointName, out var namedEndpoint))
        {
            endpointConfig = namedEndpoint;
        }
        else if (config.DefaultEndpoint != null)
        {
            endpointConfig = config.DefaultEndpoint;
        }

        if (endpointConfig?.BaseAddress == null)
        {
            throw new InvalidOperationException(
                $"No valid endpoint configuration found for tenant {config.TenantId}. " +
                $"Requested endpoint: {endpointName ?? "default"}");
        }

        // Get or create a handler for this tenant/endpoint combination
        var handler = _handlerCache.GetOrCreateHandler(config.TenantId!, endpointName);

        // Create the HttpClient
        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = endpointConfig.BaseAddress,
            Timeout = endpointConfig.Timeout ?? config.Timeout ?? TimeSpan.FromSeconds(100)
        };

        // Apply default headers from tenant configuration
        if (config.DefaultHeaders != null)
        {
            foreach (var header in config.DefaultHeaders)
            {
                if (!httpClient.DefaultRequestHeaders.Contains(header.Key))
                {
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }
        }

        // Apply endpoint-specific headers (override tenant defaults)
        if (endpointConfig.Headers != null)
        {
            foreach (var header in endpointConfig.Headers)
            {
                if (httpClient.DefaultRequestHeaders.Contains(header.Key))
                {
                    httpClient.DefaultRequestHeaders.Remove(header.Key);
                }
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        _logger.LogDebug(
            "HttpClient created for tenant {TenantId} with base address {BaseAddress}, timeout {Timeout}",
            config.TenantId,
            httpClient.BaseAddress,
            httpClient.Timeout);

        return httpClient;
    }
}

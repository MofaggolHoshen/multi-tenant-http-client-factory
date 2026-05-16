using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.DependencyInjection;

namespace MultiTenantHttpClientFactory;

/// <summary>
/// ASP.NET Core middleware that resolves the tenant from the current request
/// and populates the scoped ITenantContext.
/// </summary>
internal class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly TenantResolutionOptions _options;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger, TenantResolutionOptions? options = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new TenantResolutionOptions();
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantResolver resolver,
        ITenantConfigurationProvider configProvider,
        ITenantContext tenantContext)
    {
        try
        {
            var tenantId = await resolver.ResolveAsync(context);

            if (string.IsNullOrEmpty(tenantId))
            {
                _logger.LogWarning("Failed to resolve tenant from request");

                if (_options.FailureMode == TenantResolutionFailureMode.ReturnBadRequest)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Tenant could not be resolved from the request.");
                    return;
                }
                else if (_options.FailureMode == TenantResolutionFailureMode.InvokeCustomHandler && _options.CustomFailureHandler != null)
                {
                    await _options.CustomFailureHandler(context);
                    return;
                }
                // Otherwise: PassThrough (continue to next middleware)
            }
            else
            {
                try
                {
                    var config = await configProvider.GetConfigurationAsync(tenantId);
                    if (config != null)
                    {
                        tenantContext.TenantId = tenantId;
                        tenantContext.Configuration = config;
                        _logger.LogInformation("Tenant {TenantId} resolved and configured for request", tenantId);
                    }
                    else
                    {
                        _logger.LogWarning("Tenant {TenantId} resolved but configuration not found", tenantId);

                        if (_options.FailureMode == TenantResolutionFailureMode.ReturnBadRequest)
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsync($"Tenant '{tenantId}' configuration not found.");
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading configuration for tenant {TenantId}", tenantId);

                    if (_options.FailureMode == TenantResolutionFailureMode.ReturnBadRequest)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsync("Error loading tenant configuration.");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in tenant resolution middleware");

            if (_options.FailureMode == TenantResolutionFailureMode.ReturnBadRequest)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("An error occurred during tenant resolution.");
                return;
            }
        }

        await _next(context);
    }
}

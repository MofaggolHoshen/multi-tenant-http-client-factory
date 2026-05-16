using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MultiTenantHttpClientFactory.Abstractions;

namespace MultiTenantHttpClientFactory.DependencyInjection;

/// <summary>
/// Extension methods for IApplicationBuilder to use multi-tenant middleware.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the tenant resolution middleware to the request pipeline.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(
        this IApplicationBuilder app,
        TenantResolutionOptions? options = null)
    {
        if (app == null)
            throw new ArgumentNullException(nameof(app));

        options ??= new TenantResolutionOptions();

        app.UseMiddleware<TenantResolutionMiddleware>(options);
        return app;
    }
}

/// <summary>
/// Options for configuring tenant resolution failure behavior.
/// </summary>
public class TenantResolutionOptions
{
    /// <summary>
    /// Determines how the middleware behaves when tenant resolution fails.
    /// </summary>
    public TenantResolutionFailureMode FailureMode { get; set; } = TenantResolutionFailureMode.PassThrough;

    /// <summary>
    /// Custom handler to invoke when tenant resolution fails.
    /// </summary>
    public Func<HttpContext, Task>? CustomFailureHandler { get; set; }
}

/// <summary>
/// Enum indicating how to handle tenant resolution failures.
/// </summary>
public enum TenantResolutionFailureMode
{
    /// <summary>
    /// Allow the request to continue to the next middleware.
    /// </summary>
    PassThrough = 0,

    /// <summary>
    /// Return a 400 Bad Request response.
    /// </summary>
    ReturnBadRequest = 1,

    /// <summary>
    /// Invoke a custom handler delegate.
    /// </summary>
    InvokeCustomHandler = 2
}

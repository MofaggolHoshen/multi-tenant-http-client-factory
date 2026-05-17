using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Configuration;

/// <summary>
/// Validates and normalizes tenant configuration, especially endpoint references.
/// </summary>
internal static class TenantConfigurationValidator
{
    /// <summary>
    /// Validates and normalizes a tenant configuration.
    /// Ensures DefaultEndpointName references a valid endpoint and auto-infers for single-endpoint tenants.
    /// </summary>
    /// <param name="config">The tenant configuration to validate.</param>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    public static void ValidateAndNormalize(TenantConfiguration config, ILogger? logger = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var tenantId = config.TenantId ?? "unknown";

        // Rule 1: At least one endpoint must exist
        if (config.Endpoints == null || config.Endpoints.Count == 0)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenantId}' has no endpoints configured. At least one endpoint is required.");
        }

        // Rule 2: If DefaultEndpointName is specified, it must exist in Endpoints
        if (!string.IsNullOrWhiteSpace(config.DefaultEndpointName))
        {
            if (!config.Endpoints.ContainsKey(config.DefaultEndpointName))
            {
                throw new InvalidOperationException(
                    $"Tenant '{tenantId}' references DefaultEndpointName '{config.DefaultEndpointName}', " +
                    $"but no endpoint with that name exists. Available endpoints: {string.Join(", ", config.Endpoints.Keys)}");
            }

            logger?.LogDebug(
                "Tenant {TenantId}: DefaultEndpointName '{DefaultEndpointName}' validated successfully",
                tenantId,
                config.DefaultEndpointName);
            return;
        }

        // Rule 3: If DefaultEndpointName is null/empty and exactly one endpoint exists, auto-infer
        if (config.Endpoints.Count == 1)
        {
            var singleEndpointName = config.Endpoints.Keys.First();
            config.DefaultEndpointName = singleEndpointName;

            logger?.LogInformation(
                "Tenant {TenantId}: DefaultEndpointName not specified. Auto-inferred to '{EndpointName}' (single endpoint)",
                tenantId,
                singleEndpointName);
            return;
        }

        // Rule 4: If DefaultEndpointName is null/empty and multiple endpoints exist, fail
        throw new InvalidOperationException(
            $"Tenant '{tenantId}' has {config.Endpoints.Count} endpoints but no DefaultEndpointName specified. " +
            $"You must explicitly set DefaultEndpointName to one of: {string.Join(", ", config.Endpoints.Keys)}");
    }
}

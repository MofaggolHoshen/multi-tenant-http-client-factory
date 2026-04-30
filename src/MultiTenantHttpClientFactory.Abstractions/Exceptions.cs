using System;

namespace MultiTenantHttpClientFactory.Abstractions;

/// <summary>
/// Thrown when a requested tenant does not exist in the store.
/// </summary>
public class TenantNotFoundException : Exception
{
    public string TenantId { get; }

    public TenantNotFoundException(string tenantId)
        : base($"Tenant '{tenantId}' was not found in the tenant store.")
    {
        TenantId = tenantId;
    }

    public TenantNotFoundException(string tenantId, Exception innerException)
        : base($"Tenant '{tenantId}' was not found in the tenant store.", innerException)
    {
        TenantId = tenantId;
    }
}

/// <summary>
/// Thrown when implicit tenant resolution fails (no middleware or no resolver matched).
/// </summary>
public class TenantNotResolvedException : Exception
{
    public TenantNotResolvedException()
        : base("No tenant could be resolved from the current context. Ensure TenantResolutionMiddleware is configured and a resolver matched.")
    {
    }

    public TenantNotResolvedException(string message)
        : base(message)
    {
    }

    public TenantNotResolvedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Thrown when a certificate fails to load, with context about which tenant/config caused it.
/// </summary>
public class CertificateLoadException : Exception
{
    public string? TenantId { get; }
    public string? CertificateSource { get; }

    public CertificateLoadException(string message, string? tenantId = null, string? certificateSource = null)
        : base(message)
    {
        TenantId = tenantId;
        CertificateSource = certificateSource;
    }

    public CertificateLoadException(string message, Exception innerException, string? tenantId = null, string? certificateSource = null)
        : base(message, innerException)
    {
        TenantId = tenantId;
        CertificateSource = certificateSource;
    }
}

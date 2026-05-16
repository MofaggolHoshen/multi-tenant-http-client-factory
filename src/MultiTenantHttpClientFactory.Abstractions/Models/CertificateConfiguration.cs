using System;

namespace MultiTenantHttpClientFactory.Abstractions.Models;

/// <summary>
/// Describes how to obtain a client certificate for a tenant or endpoint.
/// </summary>
public class CertificateConfiguration
{
    /// <summary>
    /// The type of certificate source.
    /// </summary>
    public CertificateType Type { get; set; }

    /// <summary>
    /// File path for file-based certificates (relative or absolute).
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Password for encrypted certificates (e.g., .pfx files).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Thumbprint for certificates in the local store.
    /// </summary>
    public string? Thumbprint { get; set; }

    /// <summary>
    /// Base64-encoded certificate data (inline).
    /// </summary>
    public string? Base64Data { get; set; }

    /// <summary>
    /// Azure Key Vault URI for Key Vault-based certificates.
    /// </summary>
    public Uri? KeyVaultUri { get; set; }

    /// <summary>
    /// Certificate name in Azure Key Vault.
    /// </summary>
    public string? CertificateName { get; set; }
}

/// <summary>
/// Enum representing different certificate sources.
/// </summary>
public enum CertificateType
{
    /// <summary>
    /// Certificate loaded from a file path.
    /// </summary>
    File = 0,

    /// <summary>
    /// Certificate from the local Windows certificate store.
    /// </summary>
    Store = 1,

    /// <summary>
    /// Certificate provided as base64-encoded data.
    /// </summary>
    Base64 = 2,

    /// <summary>
    /// Certificate from Azure Key Vault.
    /// </summary>
    KeyVault = 3
}

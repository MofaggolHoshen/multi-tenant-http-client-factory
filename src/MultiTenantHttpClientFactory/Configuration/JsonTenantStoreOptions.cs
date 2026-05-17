using System;
using System.Collections.Generic;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace MultiTenantHttpClientFactory.Configuration;

internal sealed class JsonTenantStoreOptions
{
    public Dictionary<string, TenantConfiguration> Tenants { get; } = new(StringComparer.OrdinalIgnoreCase);
}

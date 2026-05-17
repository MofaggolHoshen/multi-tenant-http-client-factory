using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;

namespace SampleGatewayWithDb.Data;

/// <summary>
/// ITenantStore implementation using Entity Framework Core.
/// Registered as Singleton; uses IDbContextFactory to safely create short-lived
/// DbContext instances per operation without depending on the scoped DI container.
/// </summary>
public class DatabaseTenantStore : ITenantStore
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private CancellationTokenSource _changeTokenSource = new();

    public DatabaseTenantStore(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<TenantConfiguration?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var tenantEntity = await context.Tenants
            .AsNoTracking()
            .Include(t => t.Endpoints)
            .ThenInclude(e => e.Certificate)
            .Include(t => t.DefaultHeaders)
            .Include(t => t.Certificate)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        return tenantEntity == null ? null : MapToTenantConfiguration(tenantEntity);
    }

    public async Task<IReadOnlyList<TenantConfiguration>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var tenantEntities = await context.Tenants
            .AsNoTracking()
            .Include(t => t.Endpoints)
            .ThenInclude(e => e.Certificate)
            .Include(t => t.DefaultHeaders)
            .Include(t => t.Certificate)
            .OrderBy(t => t.TenantId)
            .ToListAsync(cancellationToken);

        return tenantEntities
            .Select(MapToTenantConfiguration)
            .ToList()
            .AsReadOnly();
    }

    public IChangeToken GetReloadToken()
    {
        return new CancellationChangeToken(_changeTokenSource.Token);
    }

    /// <summary>
    /// Signal that tenant data has changed to invalidate the configuration cache.
    /// </summary>
    public void InvalidateCache()
    {
        var old = Interlocked.Exchange(ref _changeTokenSource, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }

    private static TenantConfiguration MapToTenantConfiguration(TenantEntity entity)
    {
        var config = new TenantConfiguration
        {
            TenantId = entity.TenantId,
            DefaultHeaders = entity.DefaultHeaders.ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase),
            Timeout = ParseTimespan(entity.TimeoutSeconds),
            HandlerLifetime = ParseTimespan(entity.HandlerLifetimeSeconds),
        };

        var endpoints = entity.Endpoints.ToDictionary(
            e => e.Name,
            e => new EndpointConfiguration
            {
                BaseAddress = string.IsNullOrEmpty(e.BaseAddress) ? null : new Uri(e.BaseAddress),
                Timeout = ParseTimespan(e.TimeoutSeconds),
                Certificate = MapCertificateConfiguration(e.Certificate),
            });

        config.Endpoints = endpoints;

        if (!string.IsNullOrEmpty(entity.DefaultEndpointName) && endpoints.TryGetValue(entity.DefaultEndpointName, out var defaultEndpoint))
        {
            config.DefaultEndpoint = defaultEndpoint;
        }
        else if (endpoints.Count > 0)
        {
            config.DefaultEndpoint = endpoints.First().Value;
        }

        config.Certificate = MapCertificateConfiguration(entity.Certificate);

        return config;
    }

    private static CertificateConfiguration? MapCertificateConfiguration(CertificateEntity? entity)
    {
        if (entity == null)
        {
            return null;
        }

        var type = Enum.TryParse<CertificateType>(entity.Type, ignoreCase: true, out var parsedType)
            ? parsedType
            : CertificateType.File;

        return new CertificateConfiguration
        {
            Type = type,
            Path = entity.Path,
            Password = entity.Password,
            Base64Data = entity.Data,
        };
    }

    private static TimeSpan? ParseTimespan(string? value)
    {
        if (string.IsNullOrEmpty(value) || !double.TryParse(value, out var seconds))
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }
}

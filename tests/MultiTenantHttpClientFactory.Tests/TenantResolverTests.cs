using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.TenantResolution;

namespace MultiTenantHttpClientFactory.Tests;

public class TenantResolverTests
{
    // ─── HeaderTenantResolver ───────────────────────────────────────────────

    [Fact]
    public async Task HeaderResolver_ReturnsValue_WhenHeaderPresent()
    {
        var resolver = new HeaderTenantResolver();
        var ctx = CreateContext(headers: new Dictionary<string, string> { ["X-Tenant-Id"] = "acme" });

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("acme", result);
    }

    [Fact]
    public async Task HeaderResolver_ReturnsNull_WhenHeaderMissing()
    {
        var resolver = new HeaderTenantResolver();
        var ctx = CreateContext();

        var result = await resolver.ResolveAsync(ctx);

        Assert.Null(result);
    }

    [Fact]
    public async Task HeaderResolver_UsesCustomHeaderName()
    {
        var resolver = new HeaderTenantResolver("X-My-Tenant");
        var ctx = CreateContext(headers: new Dictionary<string, string> { ["X-My-Tenant"] = "beta" });

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("beta", result);
    }

    [Fact]
    public async Task HeaderResolver_ReturnsNull_WhenHeaderEmpty()
    {
        var resolver = new HeaderTenantResolver();
        var ctx = CreateContext(headers: new Dictionary<string, string> { ["X-Tenant-Id"] = "" });

        var result = await resolver.ResolveAsync(ctx);

        Assert.Null(result);
    }

    [Fact]
    public async Task HeaderResolver_ReturnsNull_WhenContextNull()
    {
        var resolver = new HeaderTenantResolver();
        var result = await resolver.ResolveAsync(null!);
        Assert.Null(result);
    }

    // ─── RouteTenantResolver ─────────────────────────────────────────────────

    [Fact]
    public async Task RouteResolver_ReturnsValue_WhenRouteParamPresent()
    {
        var resolver = new RouteTenantResolver();
        var ctx = CreateContext(routeValues: new RouteValueDictionary { ["tenantId"] = "gamma" });

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("gamma", result);
    }

    [Fact]
    public async Task RouteResolver_ReturnsNull_WhenRouteParamMissing()
    {
        var resolver = new RouteTenantResolver();
        var ctx = CreateContext();

        var result = await resolver.ResolveAsync(ctx);

        Assert.Null(result);
    }

    [Fact]
    public async Task RouteResolver_UsesCustomParamName()
    {
        var resolver = new RouteTenantResolver("tid");
        var ctx = CreateContext(routeValues: new RouteValueDictionary { ["tid"] = "delta" });

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("delta", result);
    }

    // ─── SubdomainTenantResolver ──────────────────────────────────────────────

    [Fact]
    public async Task SubdomainResolver_ReturnsFirstSegment()
    {
        var resolver = new SubdomainTenantResolver();
        var ctx = CreateContext(host: "acme.api.example.com");

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("acme", result);
    }

    [Fact]
    public async Task SubdomainResolver_ReturnsNull_ForSingleSegmentHost()
    {
        var resolver = new SubdomainTenantResolver();
        var ctx = CreateContext(host: "localhost");

        var result = await resolver.ResolveAsync(ctx);

        Assert.Null(result);
    }

    [Fact]
    public async Task SubdomainResolver_ReturnsNull_ForIPAddress()
    {
        var resolver = new SubdomainTenantResolver();
        var ctx = CreateContext(host: "127.0.0.1:5000");

        var result = await resolver.ResolveAsync(ctx);

        Assert.Null(result);
    }

    [Fact]
    public async Task SubdomainResolver_StripsBaseDomain()
    {
        var resolver = new SubdomainTenantResolver(baseDomain: "example.com");
        var ctx = CreateContext(host: "acme.example.com");

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("acme", result);
    }

    [Fact]
    public async Task SubdomainResolver_ReturnsNull_WhenHostMatchesBaseDomainExactly()
    {
        var resolver = new SubdomainTenantResolver(baseDomain: "example.com");
        var ctx = CreateContext(host: "example.com");

        var result = await resolver.ResolveAsync(ctx);

        Assert.Null(result);
    }

    // ─── ClaimsTenantResolver ─────────────────────────────────────────────────

    [Fact]
    public async Task ClaimsResolver_ReturnsValue_WhenClaimPresent()
    {
        var resolver = new ClaimsTenantResolver();
        var ctx = CreateContext(claims: new[] { new Claim("tenant_id", "epsilon") });

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("epsilon", result);
    }

    [Fact]
    public async Task ClaimsResolver_ReturnsNull_WhenClaimMissing()
    {
        var resolver = new ClaimsTenantResolver();
        var ctx = CreateContext();

        var result = await resolver.ResolveAsync(ctx);

        Assert.Null(result);
    }

    [Fact]
    public async Task ClaimsResolver_UsesCustomClaimType()
    {
        var resolver = new ClaimsTenantResolver("http://schemas.company.com/tenant");
        var ctx = CreateContext(claims: new[] { new Claim("http://schemas.company.com/tenant", "zeta") });

        var result = await resolver.ResolveAsync(ctx);

        Assert.Equal("zeta", result);
    }

    // ─── CompositeTenantResolver ──────────────────────────────────────────────

    [Fact]
    public async Task CompositeResolver_ReturnsFirstMatch()
    {
        var headerResolver = new HeaderTenantResolver();
        var routeResolver = new RouteTenantResolver();

        var composite = new CompositeTenantResolver(
            new ITenantResolver[] { headerResolver, routeResolver },
            NullLogger<CompositeTenantResolver>.Instance);

        // Both header and route have values – header wins (first)
        var ctx = CreateContext(
            headers: new Dictionary<string, string> { ["X-Tenant-Id"] = "header-tenant" },
            routeValues: new RouteValueDictionary { ["tenantId"] = "route-tenant" });

        var result = await composite.ResolveAsync(ctx);

        Assert.Equal("header-tenant", result);
    }

    [Fact]
    public async Task CompositeResolver_FallsBackToSecondResolver()
    {
        var headerResolver = new HeaderTenantResolver();
        var routeResolver = new RouteTenantResolver();

        var composite = new CompositeTenantResolver(
            new ITenantResolver[] { headerResolver, routeResolver },
            NullLogger<CompositeTenantResolver>.Instance);

        // Only route has a value
        var ctx = CreateContext(routeValues: new RouteValueDictionary { ["tenantId"] = "route-tenant" });

        var result = await composite.ResolveAsync(ctx);

        Assert.Equal("route-tenant", result);
    }

    [Fact]
    public async Task CompositeResolver_ReturnsNull_WhenNoResolverMatches()
    {
        var headerResolver = new HeaderTenantResolver();

        var composite = new CompositeTenantResolver(
            new ITenantResolver[] { headerResolver },
            NullLogger<CompositeTenantResolver>.Instance);

        var ctx = CreateContext();
        var result = await composite.ResolveAsync(ctx);

        Assert.Null(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static DefaultHttpContext CreateContext(
        Dictionary<string, string>? headers = null,
        RouteValueDictionary? routeValues = null,
        string? host = null,
        IEnumerable<Claim>? claims = null)
    {
        var ctx = new DefaultHttpContext();

        if (headers != null)
            foreach (var (k, v) in headers)
                ctx.Request.Headers[k] = v;

        if (routeValues != null)
        {
            var routeData = new RouteData();
            foreach (var (k, v) in routeValues)
                routeData.Values[k] = v;
            ctx.Features.Set<IRoutingFeature>(new RoutingFeature { RouteData = routeData });
        }

        if (host != null)
            ctx.Request.Host = new HostString(host);

        if (claims != null)
        {
            var identity = new ClaimsIdentity(claims, "test");
            ctx.User = new ClaimsPrincipal(identity);
        }

        return ctx;
    }

    private class RoutingFeature : IRoutingFeature
    {
        public RouteData RouteData { get; set; } = new RouteData();
    }
}

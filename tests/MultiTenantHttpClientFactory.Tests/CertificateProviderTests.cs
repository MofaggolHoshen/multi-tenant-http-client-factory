using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Certificates;

namespace MultiTenantHttpClientFactory.Tests;

public class CertificateProviderTests
{
    // ─── CompositeCertificateProvider ─────────────────────────────────────────

    [Fact]
    public async Task CompositeCertificateProvider_ThrowsNotSupported_WhenNoProvidersRegistered()
    {
        var provider = new CompositeCertificateProvider(NullLogger<CompositeCertificateProvider>.Instance);
        var config = new CertificateConfiguration { Type = CertificateType.File, Path = "nonexistent.pfx" };

        await Assert.ThrowsAsync<NotSupportedException>(() => provider.GetCertificateAsync(config));
    }

    [Fact]
    public async Task CompositeCertificateProvider_ThrowsNotSupported_WhenTypeNotRegistered()
    {
        var composite = new CompositeCertificateProvider(NullLogger<CompositeCertificateProvider>.Instance);
        var mockProvider = new MockCertificateProvider(null);
        composite.Register(CertificateType.File, mockProvider);

        var config = new CertificateConfiguration { Type = CertificateType.Store, Path = "MY\\Thumbprint" };

        await Assert.ThrowsAsync<NotSupportedException>(() => composite.GetCertificateAsync(config));
    }

    [Fact]
    public async Task CompositeCertificateProvider_ReturnsCertificate_FromProvider()
    {
        var composite = new CompositeCertificateProvider(NullLogger<CompositeCertificateProvider>.Instance);
        var cert = new X509Certificate2();
        var provider1 = new MockCertificateProvider(cert);

        composite.Register(CertificateType.File, provider1);

        var config = new CertificateConfiguration { Type = CertificateType.File };
        var result = await composite.GetCertificateAsync(config);

        Assert.Same(cert, result);
        Assert.True(provider1.WasCalled);
    }

    [Fact]
    public async Task CompositeCertificateProvider_ReturnsNull_WhenProviderReturnsNull()
    {
        var composite = new CompositeCertificateProvider(NullLogger<CompositeCertificateProvider>.Instance);
        var provider = new MockCertificateProvider(null);
        composite.Register(CertificateType.File, provider);

        var config = new CertificateConfiguration { Type = CertificateType.File };
        var result = await composite.GetCertificateAsync(config);

        Assert.Null(result);
        Assert.True(provider.WasCalled);
    }

    [Fact]
    public async Task CompositeCertificateProvider_ReturnsNull_WhenConfigIsNull()
    {
        var composite = new CompositeCertificateProvider(NullLogger<CompositeCertificateProvider>.Instance);

        var result = await composite.GetCertificateAsync(null);

        Assert.Null(result);
    }

    // ─── FileCertificateProvider ──────────────────────────────────────────────

    [Fact]
    public async Task FileCertificateProvider_ReturnsNull_WhenTypeNotFile()
    {
        var provider = new FileCertificateProvider(NullLogger<FileCertificateProvider>.Instance);
        var config = new CertificateConfiguration { Type = CertificateType.Base64 };

        var result = await provider.GetCertificateAsync(config);

        Assert.Null(result);
    }

    [Fact]
    public async Task FileCertificateProvider_ReturnsNull_WhenPathNotProvided()
    {
        var provider = new FileCertificateProvider(NullLogger<FileCertificateProvider>.Instance);
        var config = new CertificateConfiguration { Type = CertificateType.File };

        var result = await provider.GetCertificateAsync(config);

        Assert.Null(result);
    }

    [Fact]
    public async Task FileCertificateProvider_ThrowsCertificateLoadException_WhenFileDoesNotExist()
    {
        var provider = new FileCertificateProvider(NullLogger<FileCertificateProvider>.Instance);
        var config = new CertificateConfiguration
        {
            Type = CertificateType.File,
            Path = "/nonexistent/path/cert.pfx"
        };

        await Assert.ThrowsAsync<CertificateLoadException>(() => provider.GetCertificateAsync(config));
    }

    // ─── Base64CertificateProvider ────────────────────────────────────────────

    [Fact]
    public async Task Base64CertificateProvider_ReturnsNull_WhenTypeNotBase64()
    {
        var provider = new Base64CertificateProvider(NullLogger<Base64CertificateProvider>.Instance);
        var config = new CertificateConfiguration { Type = CertificateType.File };

        var result = await provider.GetCertificateAsync(config);

        Assert.Null(result);
    }

    [Fact]
    public async Task Base64CertificateProvider_ReturnsNull_WhenDataNotProvided()
    {
        var provider = new Base64CertificateProvider(NullLogger<Base64CertificateProvider>.Instance);
        var config = new CertificateConfiguration { Type = CertificateType.Base64 };

        var result = await provider.GetCertificateAsync(config);

        Assert.Null(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private class MockCertificateProvider : ICertificateProvider
    {
        private readonly X509Certificate2? _certificate;
        public bool WasCalled { get; private set; }

        public MockCertificateProvider(X509Certificate2? certificate)
        {
            _certificate = certificate;
        }

        public Task<X509Certificate2?> GetCertificateAsync(CertificateConfiguration config, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(_certificate);
        }
    }
}

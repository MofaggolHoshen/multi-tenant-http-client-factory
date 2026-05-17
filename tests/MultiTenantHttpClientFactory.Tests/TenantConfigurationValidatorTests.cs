using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using MultiTenantHttpClientFactory.Abstractions.Models;
using MultiTenantHttpClientFactory.Configuration;

namespace MultiTenantHttpClientFactory.Tests;

public class TenantConfigurationValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_ThrowsWhenNoEndpoints()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = "test-tenant",
            Endpoints = new Dictionary<string, EndpointConfiguration>()
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TenantConfigurationValidator.ValidateAndNormalize(config));

        Assert.Contains("test-tenant", ex.Message);
        Assert.Contains("no endpoints configured", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_ThrowsWhenDefaultEndpointNameNotFound()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = "test-tenant",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["api"] = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") }
            },
            DefaultEndpointName = "nonexistent"
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TenantConfigurationValidator.ValidateAndNormalize(config));

        Assert.Contains("test-tenant", ex.Message);
        Assert.Contains("nonexistent", ex.Message);
        Assert.Contains("api", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_ThrowsWhenMultipleEndpointsAndNoDefault()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = "test-tenant",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["api"] = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") },
                ["webhook"] = new EndpointConfiguration { BaseAddress = new Uri("https://webhook.example.com") }
            },
            DefaultEndpointName = null
        };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TenantConfigurationValidator.ValidateAndNormalize(config));

        Assert.Contains("test-tenant", ex.Message);
        Assert.Contains("2 endpoints", ex.Message);
        Assert.Contains("api", ex.Message);
        Assert.Contains("webhook", ex.Message);
    }

    [Fact]
    public void ValidateAndNormalize_InfersSingleEndpoint()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = "test-tenant",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["only-one"] = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") }
            },
            DefaultEndpointName = null
        };

        // Act
        TenantConfigurationValidator.ValidateAndNormalize(config, NullLogger.Instance);

        // Assert
        Assert.Equal("only-one", config.DefaultEndpointName);
    }

    [Fact]
    public void ValidateAndNormalize_AcceptsExplicitValidDefault()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = "test-tenant",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["api"] = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") },
                ["webhook"] = new EndpointConfiguration { BaseAddress = new Uri("https://webhook.example.com") }
            },
            DefaultEndpointName = "api"
        };

        // Act
        TenantConfigurationValidator.ValidateAndNormalize(config, NullLogger.Instance);

        // Assert
        Assert.Equal("api", config.DefaultEndpointName);
    }

    [Fact]
    public void ValidateAndNormalize_ThrowsArgumentNullException_WhenConfigIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TenantConfigurationValidator.ValidateAndNormalize(null!));
    }

    [Fact]
    public void ValidateAndNormalize_HandlesEmptyDefaultEndpointName()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = "test-tenant",
            Endpoints = new Dictionary<string, EndpointConfiguration>
            {
                ["single"] = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") }
            },
            DefaultEndpointName = "   " // Whitespace-only
        };

        // Act
        TenantConfigurationValidator.ValidateAndNormalize(config);

        // Assert - should infer since whitespace is treated as empty
        Assert.Equal("single", config.DefaultEndpointName);
    }

    [Fact]
    public void ValidateAndNormalize_IsCaseInsensitive()
    {
        // Arrange
        var config = new TenantConfiguration
        {
            TenantId = "test-tenant",
            Endpoints = new Dictionary<string, EndpointConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["API"] = new EndpointConfiguration { BaseAddress = new Uri("https://api.example.com") }
            },
            DefaultEndpointName = "api" // Different case
        };

        // Act
        TenantConfigurationValidator.ValidateAndNormalize(config);

        // Assert - should succeed because dictionary is case-insensitive
        Assert.Equal("api", config.DefaultEndpointName);
    }
}

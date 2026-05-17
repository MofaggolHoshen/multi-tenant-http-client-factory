using Microsoft.EntityFrameworkCore;
using MultiTenantHttpClientFactory;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.DependencyInjection;
using MultiTenantHttpClientFactory.TenantResolution;
using SampleGatewayWithDb.Data;

var builder = WebApplication.CreateBuilder(args);

// Configure Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=MultiTenantDb;Trusted_Connection=true;";

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register multi-tenant HttpClient factory
builder.Services
    .AddMultiTenantHttpClientFactory()
    .WithTenantStore<DatabaseTenantStore>()
    .AddTenantResolver(new HeaderTenantResolver())
    .ConfigureDefaultHandler(handler =>
    {
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(5);
    });

// Add logging
builder.Logging.AddConsole();

// Add swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.InitializeAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use tenant resolution middleware
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseHttpsRedirection();

// Endpoint: Proxy a request through a tenant's HTTP client
app.MapPost("/proxy/{tenantId}/{*path}", ProxyRequest)
    .WithName("ProxyRequest")
    .WithOpenApi()
    .Produces<string>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status500InternalServerError);

// Endpoint: List all configured tenants
app.MapGet("/tenants", ListTenants)
    .WithName("ListTenants")
    .WithOpenApi()
    .Produces<IEnumerable<TenantInfo>>(StatusCodes.Status200OK);

// Endpoint: Get a specific tenant
app.MapGet("/tenants/{tenantId}", GetTenant)
    .WithName("GetTenant")
    .WithOpenApi()
    .Produces<TenantInfo>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

async Task<IResult> ProxyRequest(
    string tenantId,
    string path,
    HttpContext context,
    ITenantHttpClientFactory clientFactory,
    ILogger<Program> logger)
{
    try
    {
        logger.LogInformation("Proxying request to tenant {TenantId}, path: {Path}", tenantId, path);

        using var client = clientFactory.CreateClient(tenantId);

        var requestBody = context.Request.Body.CanSeek
            ? await new StreamReader(context.Request.Body).ReadToEndAsync()
            : string.Empty;

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (!string.IsNullOrEmpty(requestBody))
        {
            request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
        }

        foreach (var (key, value) in context.Request.Headers)
        {
            if (!new[] { "Host", "Content-Length", "Transfer-Encoding" }.Contains(key))
            {
                request.Headers.TryAddWithoutValidation(key, (string?)value);
            }
        }

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();
        logger.LogInformation("Proxy response status: {StatusCode}", response.StatusCode);

        return Results.Text(responseContent, statusCode: (int)response.StatusCode);
    }
    catch (TenantNotFoundException ex)
    {
        logger.LogWarning(ex, "Tenant not found: {TenantId}", tenantId);
        return Results.BadRequest(new { error = $"Tenant '{tenantId}' not found" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error proxying request for tenant {TenantId}", tenantId);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

async Task<IResult> ListTenants(
    ITenantStore tenantStore,
    ILogger<Program> logger)
{
    try
    {
        var tenants = await tenantStore.GetAllTenantsAsync();
        var result = tenants.Select(t => new TenantInfo(
            t.TenantId ?? "Unknown",
            t.Endpoints.Count,
            t.DefaultEndpoint?.BaseAddress?.ToString() ?? "N/A"
        ));

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error listing tenants");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

async Task<IResult> GetTenant(
    string tenantId,
    ITenantStore tenantStore,
    ILogger<Program> logger)
{
    try
    {
        var tenant = await tenantStore.GetTenantAsync(tenantId);
        if (tenant == null)
        {
            return Results.NotFound(new { error = $"Tenant '{tenantId}' not found" });
        }

        var result = new TenantInfo(
            tenant.TenantId ?? "Unknown",
            tenant.Endpoints.Count,
            tenant.DefaultEndpoint?.BaseAddress?.ToString() ?? "N/A"
        );

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error getting tenant {TenantId}", tenantId);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

record TenantInfo(string TenantId, int EndpointCount, string DefaultEndpoint);

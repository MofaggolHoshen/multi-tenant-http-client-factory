using MultiTenantHttpClientFactory;
using MultiTenantHttpClientFactory.Abstractions;
using MultiTenantHttpClientFactory.DependencyInjection;
using MultiTenantHttpClientFactory.TenantResolution;

var builder = WebApplication.CreateBuilder(args);

// Register multi-tenant HttpClient factory
builder.Services
    .AddMultiTenantHttpClientFactory()
    .WithJsonConfiguration("MultiTenant:Tenants")
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use tenant resolution middleware
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseHttpsRedirection();

//https://jsonplaceholder.typicode.com/guide/

app.MapGet("/proxy1/{*path}", ProxyGetRequestResolvedWithHeader)
    .WithName("ProxyRequestHeader")
    .WithOpenApi()
    .Produces<string>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status500InternalServerError);

app.MapGet("/proxy2/{tenantId}/{*path}", ProxyGetRequestResolvedWithTenant)
    .WithName("ProxyRequestTenant")
    .WithOpenApi()
    .Produces<string>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status500InternalServerError);



app.Run();

async Task<IResult> ProxyGetRequestResolvedWithTenant(
    string tenantId,
    string path,
    HttpContext context,
    ITenantHttpClientFactory clientFactory,
    ILogger<Program> logger)
{
    try
    {
        logger.LogInformation("Proxying request to tenant {TenantId}, path: {Path}", tenantId, path);

        // Create a client for the specified tenant
        using var client = clientFactory.CreateClient(tenantId: tenantId);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        // Forward headers (excluding host-specific headers)
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

async Task<IResult> ProxyGetRequestResolvedWithHeader(
    string path,
    HttpContext context,
    ITenantHttpClientFactory clientFactory,
    ILogger<Program> logger)
{
    try
    {
        logger.LogInformation("Proxying request to tenant path: {Path}", path);

        // Create a client using tenant resolution from headers
        using var client = clientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        // Forward headers (excluding host-specific headers)
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
        logger.LogWarning(ex, "Tenant not found");
        return Results.BadRequest(new { error = "Tenant not found" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error proxying request for tenant");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using DistributedWorkerService.Configuration;
using DistributedWorkerService.Services;

namespace DistributedWorkerService.Extensions;

/// <summary>
/// Extension methods for IServiceCollection to configure services
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDistributedWorkerServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<WorkerConfiguration>(configuration.GetSection(WorkerConfiguration.SectionName));
        services.Configure<CosmosDbConfiguration>(configuration.GetSection(CosmosDbConfiguration.SectionName));
        services.Configure<ExternalApiConfiguration>(configuration.GetSection(ExternalApiConfiguration.SectionName));

        // Core services
        services.AddSingleton<ICosmosLeaseCheckpointService, CosmosLeaseCheckpointService>();
        services.AddScoped<IKeyRangeProcessingService, KeyRangeProcessingService>();

        // HTTP client for external API with resilience policies
        services.AddHttpClient<IExternalApiClient, ExternalApiClient>();

        // Background service
        services.AddHostedService<DistributedChangeProcessorWorker>();

        // Health checks
        services.AddHealthChecks()
            .AddCheck<CosmosDbHealthCheck>("cosmosdb")
            .AddCheck<ExternalApiHealthCheck>("external-api");

        return services;
    }
}

/// <summary>
/// Health check for Cosmos DB connectivity
/// </summary>
public class CosmosDbHealthCheck : IHealthCheck
{
    private readonly ICosmosLeaseCheckpointService _cosmosService;

    public CosmosDbHealthCheck(ICosmosLeaseCheckpointService cosmosService)
    {
        _cosmosService = cosmosService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get available key ranges as a simple connectivity test
            await _cosmosService.GetAvailableKeyRangesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Cosmos DB is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cosmos DB is not accessible", ex);
        }
    }
}

/// <summary>
/// Health check for External API connectivity
/// </summary>
public class ExternalApiHealthCheck : IHealthCheck
{
    private readonly IExternalApiClient _apiClient;

    public ExternalApiHealthCheck(IExternalApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to get tasks as a connectivity test
            await _apiClient.GetTasksAsync(cancellationToken);
            return HealthCheckResult.Healthy("External API is accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("External API is not accessible", ex);
        }
    }
}
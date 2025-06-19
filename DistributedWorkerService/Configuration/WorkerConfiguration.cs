namespace DistributedWorkerService.Configuration;

/// <summary>
/// Configuration settings for the distributed worker service
/// </summary>
public class WorkerConfiguration
{
    public const string SectionName = "WorkerConfiguration";

    public string WorkerId { get; set; } = Environment.MachineName + "-" + Guid.NewGuid().ToString("N")[..8];
    public int PollingIntervalSeconds { get; set; } = 30;
    public int LeaseDurationMinutes { get; set; } = 5;
    public int CheckpointUpdateIntervalSeconds { get; set; } = 30;
    public int MaxConcurrentKeyRanges { get; set; } = 10;
    public RetryPolicyConfiguration RetryPolicy { get; set; } = new();

    /// <summary>
    /// Resolves template placeholders in WorkerId
    /// </summary>
    public string ResolveWorkerId()
    {
        return WorkerId
            .Replace("{HOSTNAME}", Environment.MachineName)
            .Replace("{GUID}", Guid.NewGuid().ToString("N")[..8])
            .Replace("{PID}", Environment.ProcessId.ToString());
    }
}

/// <summary>
/// Retry policy configuration
/// </summary>
public class RetryPolicyConfiguration
{
    public int MaxRetryAttempts { get; set; } = 3;
    public int BaseDelaySeconds { get; set; } = 2;
    public int MaxDelaySeconds { get; set; } = 60;
}

/// <summary>
/// Cosmos DB configuration
/// </summary>
public class CosmosDbConfiguration
{
    public const string SectionName = "CosmosDb";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "DistributedWorker";
    public string ContainerName { get; set; } = "LeaseCheckpoints";
    public int ThroughputRU { get; set; } = 400;
}

/// <summary>
/// External API configuration
/// </summary>
public class ExternalApiConfiguration
{
    public const string SectionName = "ExternalApi";

    public string BaseUrl { get; set; } = string.Empty;
    public string MetadataEndpoint { get; set; } = "/v1/tasks";
    public string GetChangesEndpoint { get; set; } = "/v1/changes/{taskId}";
    public OAuthConfiguration OAuth { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public int RateLimitRequestsPerSecond { get; set; } = 10;
}

/// <summary>
/// OAuth 2.0 configuration
/// </summary>
public class OAuthConfiguration
{
    public string TokenEndpoint { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}
using System.Text.Json.Serialization;

namespace DistributedWorkerService.Models;

/// <summary>
/// Represents a document in Cosmos DB that combines lease management and checkpoint data
/// </summary>
public class LeaseCheckpointDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "LeaseCheckpoint";

    [JsonPropertyName("keyRangeId")]
    public string KeyRangeId { get; set; } = string.Empty;

    [JsonPropertyName("workerId")]
    public string? WorkerId { get; set; }

    [JsonPropertyName("leaseExpiry")]
    public DateTime? LeaseExpiry { get; set; }

    [JsonPropertyName("leaseAcquiredAt")]
    public DateTime? LeaseAcquiredAt { get; set; }

    [JsonPropertyName("lastProcessedTimestamp")]
    public DateTime? LastProcessedTimestamp { get; set; }

    [JsonPropertyName("lastProcessedOffset")]
    public string? LastProcessedOffset { get; set; }

    [JsonPropertyName("processingState")]
    public ProcessingState ProcessingState { get; set; } = ProcessingState.Available;

    [JsonPropertyName("lastCheckpointUpdate")]
    public DateTime? LastCheckpointUpdate { get; set; }

    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    [JsonPropertyName("_ts")]
    public long? Timestamp { get; set; }

    /// <summary>
    /// Determines if the lease is currently valid and owned by the specified worker
    /// </summary>
    public bool IsLeaseValid(string workerId)
    {
        return WorkerId == workerId && 
               LeaseExpiry.HasValue && 
               LeaseExpiry.Value > DateTime.UtcNow &&
               ProcessingState == ProcessingState.InProgress;
    }

    /// <summary>
    /// Determines if the lease has expired and is available for acquisition
    /// </summary>
    public bool IsLeaseExpired()
    {
        return !LeaseExpiry.HasValue || 
               LeaseExpiry.Value <= DateTime.UtcNow ||
               ProcessingState == ProcessingState.Available;
    }
}

/// <summary>
/// Represents the processing state of a key range
/// </summary>
public enum ProcessingState
{
    Available,
    InProgress,
    Completed,
    Failed
}
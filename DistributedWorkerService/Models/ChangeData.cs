using System.Text.Json.Serialization;

namespace DistributedWorkerService.Models;

/// <summary>
/// Represents change data returned from the external API
/// </summary>
public class ChangeData
{
    [JsonPropertyName("changeId")]
    public string ChangeId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("offset")]
    public string Offset { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("entityId")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public Dictionary<string, object> Data { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Response wrapper for changes API
/// </summary>
public class ChangesResponse
{
    [JsonPropertyName("changes")]
    public List<ChangeData> Changes { get; set; } = new();

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }

    [JsonPropertyName("nextOffset")]
    public string? NextOffset { get; set; }

    [JsonPropertyName("keyRangeId")]
    public string KeyRangeId { get; set; } = string.Empty;

    [JsonPropertyName("fromOffset")]
    public string? FromOffset { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}

/// <summary>
/// Represents a batch of processed changes with their results
/// </summary>
public class ProcessedChangesBatch
{
    public string KeyRangeId { get; set; } = string.Empty;
    public List<ChangeData> Changes { get; set; } = new();
    public CheckpointData Checkpoint { get; set; } = new();
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
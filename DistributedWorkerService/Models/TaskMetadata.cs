using System.Text.Json.Serialization;

namespace DistributedWorkerService.Models;

/// <summary>
/// Represents task metadata returned from the external API
/// </summary>
public class TaskMetadata
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("keyRangeId")]
    public string KeyRangeId { get; set; } = string.Empty;

    [JsonPropertyName("keyRangeStart")]
    public string? KeyRangeStart { get; set; }

    [JsonPropertyName("keyRangeEnd")]
    public string? KeyRangeEnd { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("estimatedChangeCount")]
    public long EstimatedChangeCount { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 1;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Response wrapper for task metadata API
/// </summary>
public class TaskMetadataResponse
{
    [JsonPropertyName("tasks")]
    public List<TaskMetadata> Tasks { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("nextToken")]
    public string? NextToken { get; set; }
}
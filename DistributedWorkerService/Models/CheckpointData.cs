namespace DistributedWorkerService.Models;

/// <summary>
/// Represents checkpoint data for a key range
/// </summary>
public class CheckpointData
{
    public string KeyRangeId { get; set; } = string.Empty;
    public DateTime? LastProcessedTimestamp { get; set; }
    public string? LastProcessedOffset { get; set; }
    public DateTime LastCheckpointUpdate { get; set; } = DateTime.UtcNow;
    public long ProcessedCount { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
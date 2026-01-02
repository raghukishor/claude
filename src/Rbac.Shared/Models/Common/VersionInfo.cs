using Newtonsoft.Json;

namespace Rbac.Shared.Models.Common;

/// <summary>
/// Tracks version information for change detection and idempotency.
/// </summary>
public class VersionInfo
{
    /// <summary>
    /// Monotonically increasing version number, incremented on every write.
    /// </summary>
    [JsonProperty("sequenceNumber")]
    public long SequenceNumber { get; set; }

    /// <summary>
    /// High-precision timestamp of last modification.
    /// </summary>
    [JsonProperty("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Unique identifier for this specific change (idempotency key).
    /// Format: chg-{timestamp}-{random}
    /// </summary>
    [JsonProperty("changeId")]
    public string ChangeId { get; set; } = string.Empty;
}

using Newtonsoft.Json;

namespace Rbac.Shared.Models.Common;

/// <summary>
/// Tracks lifecycle state for soft delete support.
/// </summary>
public class LifecycleInfo
{
    /// <summary>
    /// Document state: Active, Deleted, PendingDelete
    /// </summary>
    [JsonProperty("state")]
    public string State { get; set; } = LifecycleState.Active;

    /// <summary>
    /// Soft delete flag for change feed visibility.
    /// </summary>
    [JsonProperty("isDeleted")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Timestamp when document was deleted.
    /// </summary>
    [JsonProperty("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// TTL in seconds (-1 = never expire, set during deletion).
    /// </summary>
    [JsonProperty("ttl")]
    public int Ttl { get; set; } = -1;
}

public static class LifecycleState
{
    public const string Active = "Active";
    public const string Deleted = "Deleted";
    public const string PendingDelete = "PendingDelete";
}

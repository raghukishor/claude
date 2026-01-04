using Newtonsoft.Json;

namespace Rbac.Shared.Models.DataPlane;

/// <summary>
/// Denormalized document for fast authorization lookups.
/// One document per principal-scope combination.
/// </summary>
public class EffectiveAccess
{
    /// <summary>
    /// Document ID. Format: ea-{principalId}-{scopeHash}
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The principal this access applies to.
    /// </summary>
    [JsonProperty("principalId")]
    public string PrincipalId { get; set; } = string.Empty;

    /// <summary>
    /// The scope this access applies to.
    /// </summary>
    [JsonProperty("scope")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the scope for document ID generation.
    /// </summary>
    [JsonProperty("scopeHash")]
    public string ScopeHash { get; set; } = string.Empty;

    /// <summary>
    /// Depth of the scope in the hierarchy (0 = root).
    /// </summary>
    [JsonProperty("scopeDepth")]
    public int ScopeDepth { get; set; }

    /// <summary>
    /// All effective roles for this principal at this scope.
    /// </summary>
    [JsonProperty("effectiveRoles")]
    public List<EffectiveRole> EffectiveRoles { get; set; } = new();

    /// <summary>
    /// Groups this principal belongs to (for group-based lookups).
    /// </summary>
    [JsonProperty("groupMemberships")]
    public List<string> GroupMemberships { get; set; } = new();

    /// <summary>
    /// Sync tracking metadata.
    /// </summary>
    [JsonProperty("_sync")]
    public SyncMetadata Sync { get; set; } = new();

    /// <summary>
    /// High-water marks for detecting stale data.
    /// </summary>
    [JsonProperty("_watermarks")]
    public Watermarks Watermarks { get; set; } = new();

    /// <summary>
    /// CosmosDB ETag for optimistic concurrency.
    /// </summary>
    [JsonProperty("_etag")]
    public string? ETag { get; set; }
}

/// <summary>
/// Sync tracking metadata for the EffectiveAccess document.
/// </summary>
public class SyncMetadata
{
    /// <summary>
    /// Local version counter, incremented on every update.
    /// </summary>
    [JsonProperty("documentVersion")]
    public long DocumentVersion { get; set; }

    /// <summary>
    /// Change IDs already applied (for deduplication).
    /// </summary>
    [JsonProperty("lastProcessedChangeIds")]
    public List<string> LastProcessedChangeIds { get; set; } = new();

    /// <summary>
    /// Timestamp of last sync.
    /// </summary>
    [JsonProperty("lastSyncedAt")]
    public DateTimeOffset LastSyncedAt { get; set; }

    /// <summary>
    /// ID of the sync batch that last modified this document.
    /// </summary>
    [JsonProperty("syncBatchId")]
    public string SyncBatchId { get; set; } = string.Empty;

    /// <summary>
    /// Sync mode: Bootstrap or Incremental.
    /// </summary>
    [JsonProperty("syncMode")]
    public string SyncMode { get; set; } = SyncModes.Incremental;

    /// <summary>
    /// LSN positions in source containers at time of sync.
    /// </summary>
    [JsonProperty("sourceCheckpoint")]
    public SourceCheckpoint SourceCheckpoint { get; set; } = new();
}

/// <summary>
/// LSN positions in source containers.
/// </summary>
public class SourceCheckpoint
{
    [JsonProperty("roleDefinitionsLsn")]
    public long RoleDefinitionsLsn { get; set; }

    [JsonProperty("roleAssignmentsLsn")]
    public long RoleAssignmentsLsn { get; set; }
}

/// <summary>
/// High-water marks for detecting stale data.
/// </summary>
public class Watermarks
{
    [JsonProperty("highWatermark")]
    public DateTimeOffset HighWatermark { get; set; }

    [JsonProperty("roleDefHighWatermark")]
    public DateTimeOffset RoleDefHighWatermark { get; set; }

    [JsonProperty("assignmentHighWatermark")]
    public DateTimeOffset AssignmentHighWatermark { get; set; }
}

public static class SyncModes
{
    public const string Bootstrap = "Bootstrap";
    public const string Incremental = "Incremental";
}

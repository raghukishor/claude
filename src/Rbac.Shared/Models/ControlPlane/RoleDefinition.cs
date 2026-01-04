using Newtonsoft.Json;
using Rbac.Shared.Models.Common;

namespace Rbac.Shared.Models.ControlPlane;

/// <summary>
/// Represents a role definition containing a collection of permissions.
/// </summary>
public class RoleDefinition
{
    /// <summary>
    /// Unique identifier for the role definition.
    /// Format: rd-{guid}
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the role.
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the role.
    /// </summary>
    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Role type: BuiltIn or Custom.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = RoleType.Custom;

    /// <summary>
    /// Collection of permissions granted by this role.
    /// </summary>
    [JsonProperty("permissions")]
    public List<Permission> Permissions { get; set; } = new();

    /// <summary>
    /// Scopes where this role can be assigned.
    /// "/" means assignable at any scope.
    /// </summary>
    [JsonProperty("assignableScopes")]
    public List<string> AssignableScopes { get; set; } = new() { "/" };

    /// <summary>
    /// Version tracking for change detection and idempotency.
    /// </summary>
    [JsonProperty("_version")]
    public VersionInfo Version { get; set; } = new();

    /// <summary>
    /// Lifecycle state for soft delete support.
    /// </summary>
    [JsonProperty("_lifecycle")]
    public LifecycleInfo Lifecycle { get; set; } = new();

    /// <summary>
    /// Audit metadata.
    /// </summary>
    [JsonProperty("_metadata")]
    public MetadataInfo Metadata { get; set; } = new();

    /// <summary>
    /// CosmosDB ETag for optimistic concurrency.
    /// </summary>
    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    /// <summary>
    /// Partition key for CosmosDB storage.
    /// </summary>
    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; } = "RoleDefinition";
}

public static class RoleType
{
    public const string BuiltIn = "BuiltIn";
    public const string Custom = "Custom";
}

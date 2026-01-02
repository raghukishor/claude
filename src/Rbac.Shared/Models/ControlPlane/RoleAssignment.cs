using Newtonsoft.Json;
using Rbac.Shared.Models.Common;

namespace Rbac.Shared.Models.ControlPlane;

/// <summary>
/// Represents a role assignment binding a principal to a role at a scope.
/// </summary>
public class RoleAssignment
{
    /// <summary>
    /// Unique identifier for the role assignment.
    /// Format: ra-{guid}
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The principal being granted access.
    /// </summary>
    [JsonProperty("principalId")]
    public string PrincipalId { get; set; } = string.Empty;

    /// <summary>
    /// Type of principal: User, Group, ServicePrincipal, ManagedIdentity.
    /// </summary>
    [JsonProperty("principalType")]
    public string PrincipalType { get; set; } = string.Empty;

    /// <summary>
    /// The role definition being assigned.
    /// </summary>
    [JsonProperty("roleDefinitionId")]
    public string RoleDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// The scope at which the role is assigned.
    /// </summary>
    [JsonProperty("scope")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Optional condition for attribute-based access control.
    /// </summary>
    [JsonProperty("condition")]
    public Condition? Condition { get; set; }

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
    /// Tracks versions of referenced entities.
    /// </summary>
    [JsonProperty("_references")]
    public ReferenceInfo References { get; set; } = new();

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
}

/// <summary>
/// Tracks versions of referenced entities for change propagation.
/// </summary>
public class ReferenceInfo
{
    /// <summary>
    /// Sequence number of the role definition at assignment time.
    /// </summary>
    [JsonProperty("roleDefinitionVersion")]
    public long RoleDefinitionVersion { get; set; }

    /// <summary>
    /// Change ID of the role definition at assignment time.
    /// </summary>
    [JsonProperty("roleDefinitionChangeId")]
    public string RoleDefinitionChangeId { get; set; } = string.Empty;
}

public static class PrincipalType
{
    public const string User = "User";
    public const string Group = "Group";
    public const string ServicePrincipal = "ServicePrincipal";
    public const string ManagedIdentity = "ManagedIdentity";
}

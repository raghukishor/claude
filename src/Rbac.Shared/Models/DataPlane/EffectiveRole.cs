using Newtonsoft.Json;
using Rbac.Shared.Models.Common;

namespace Rbac.Shared.Models.DataPlane;

/// <summary>
/// A denormalized role entry in the EffectiveAccess document.
/// Contains all information needed for authorization without additional lookups.
/// </summary>
public class EffectiveRole
{
    /// <summary>
    /// The role definition ID.
    /// </summary>
    [JsonProperty("roleDefinitionId")]
    public string RoleDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the role (denormalized for convenience).
    /// </summary>
    [JsonProperty("roleName")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// The role assignment that granted this access.
    /// </summary>
    [JsonProperty("assignmentId")]
    public string AssignmentId { get; set; } = string.Empty;

    /// <summary>
    /// The scope where the assignment was made.
    /// </summary>
    [JsonProperty("assignmentScope")]
    public string AssignmentScope { get; set; } = string.Empty;

    /// <summary>
    /// Whether this role is inherited from a parent scope.
    /// </summary>
    [JsonProperty("inherited")]
    public bool Inherited { get; set; }

    /// <summary>
    /// Optional condition for attribute-based access control.
    /// </summary>
    [JsonProperty("condition")]
    public Condition? Condition { get; set; }

    /// <summary>
    /// The permissions granted by this role (denormalized).
    /// </summary>
    [JsonProperty("permissions")]
    public Permission Permissions { get; set; } = new();

    /// <summary>
    /// Tracks the source versions that produced this entry.
    /// </summary>
    [JsonProperty("_sourceVersions")]
    public SourceVersions SourceVersions { get; set; } = new();
}

/// <summary>
/// Tracks the exact version of source documents that produced this role entry.
/// </summary>
public class SourceVersions
{
    /// <summary>
    /// Sequence number of the role assignment.
    /// </summary>
    [JsonProperty("assignmentSequence")]
    public long AssignmentSequence { get; set; }

    /// <summary>
    /// Change ID of the role assignment.
    /// </summary>
    [JsonProperty("assignmentChangeId")]
    public string AssignmentChangeId { get; set; } = string.Empty;

    /// <summary>
    /// Sequence number of the role definition.
    /// </summary>
    [JsonProperty("roleDefSequence")]
    public long RoleDefSequence { get; set; }

    /// <summary>
    /// Change ID of the role definition.
    /// </summary>
    [JsonProperty("roleDefChangeId")]
    public string RoleDefChangeId { get; set; } = string.Empty;
}

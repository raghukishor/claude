using Newtonsoft.Json;

namespace Rbac.Shared.Models.Requests;

/// <summary>
/// Request DTO for conditions in role assignments.
/// </summary>
public class ConditionRequest
{
    [JsonProperty("expression")]
    public string Expression { get; set; } = string.Empty;

    [JsonProperty("version")]
    public string? Version { get; set; }
}

/// <summary>
/// Request to create a role assignment.
/// </summary>
public class CreateRoleAssignmentRequest
{
    /// <summary>
    /// The principal to grant access to.
    /// </summary>
    [JsonProperty("principalId")]
    public string PrincipalId { get; set; } = string.Empty;

    /// <summary>
    /// Type of principal: User, Group, ServicePrincipal, ManagedIdentity.
    /// </summary>
    [JsonProperty("principalType")]
    public string PrincipalType { get; set; } = string.Empty;

    /// <summary>
    /// The role definition to assign.
    /// </summary>
    [JsonProperty("roleDefinitionId")]
    public string RoleDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Optional condition for attribute-based access control.
    /// </summary>
    [JsonProperty("condition")]
    public ConditionRequest? Condition { get; set; }

    /// <summary>
    /// Optional description for the assignment.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }
}

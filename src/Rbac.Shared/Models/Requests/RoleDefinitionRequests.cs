using Newtonsoft.Json;

namespace Rbac.Shared.Models.Requests;

/// <summary>
/// Request DTO for permissions in role definitions.
/// </summary>
public class PermissionRequest
{
    [JsonProperty("actions")]
    public List<string> Actions { get; set; } = new();

    [JsonProperty("notActions")]
    public List<string> NotActions { get; set; } = new();

    [JsonProperty("dataActions")]
    public List<string> DataActions { get; set; } = new();

    [JsonProperty("notDataActions")]
    public List<string> NotDataActions { get; set; } = new();
}

/// <summary>
/// Request to create a role definition.
/// </summary>
public class CreateRoleDefinitionRequest
{
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
    /// Collection of permissions granted by this role.
    /// </summary>
    [JsonProperty("permissions")]
    public List<PermissionRequest> Permissions { get; set; } = new();

    /// <summary>
    /// Scopes where this role can be assigned.
    /// </summary>
    [JsonProperty("assignableScopes")]
    public List<string> AssignableScopes { get; set; } = new() { "/" };
}

/// <summary>
/// Request to update a role definition.
/// </summary>
public class UpdateRoleDefinitionRequest
{
    /// <summary>
    /// Display name of the role.
    /// </summary>
    [JsonProperty("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Description of the role.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Collection of permissions granted by this role.
    /// </summary>
    [JsonProperty("permissions")]
    public List<PermissionRequest>? Permissions { get; set; }

    /// <summary>
    /// Scopes where this role can be assigned.
    /// </summary>
    [JsonProperty("assignableScopes")]
    public List<string>? AssignableScopes { get; set; }
}

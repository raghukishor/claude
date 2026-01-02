using Newtonsoft.Json;

namespace Rbac.Shared.Models.Requests;

/// <summary>
/// Request to check if a subject has permission to perform an action on a resource.
/// </summary>
public class CheckAccessRequest
{
    /// <summary>
    /// The subject (principal) requesting access.
    /// </summary>
    [JsonProperty("subject")]
    public SubjectInfo Subject { get; set; } = new();

    /// <summary>
    /// The resource being accessed.
    /// </summary>
    [JsonProperty("resource")]
    public ResourceInfo Resource { get; set; } = new();

    /// <summary>
    /// The action being performed.
    /// </summary>
    [JsonProperty("action")]
    public ActionInfo Action { get; set; } = new();
}

/// <summary>
/// Information about the subject requesting access.
/// </summary>
public class SubjectInfo
{
    /// <summary>
    /// The principal ID.
    /// </summary>
    [JsonProperty("principalId")]
    public string PrincipalId { get; set; } = string.Empty;

    /// <summary>
    /// Type of principal: User, Group, ServicePrincipal, ManagedIdentity.
    /// </summary>
    [JsonProperty("principalType")]
    public string PrincipalType { get; set; } = string.Empty;

    /// <summary>
    /// Groups the principal belongs to.
    /// </summary>
    [JsonProperty("groups")]
    public List<string> Groups { get; set; } = new();
}

/// <summary>
/// Information about the resource being accessed.
/// </summary>
public class ResourceInfo
{
    /// <summary>
    /// The resource scope.
    /// </summary>
    [JsonProperty("scope")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The resource type.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Resource attributes for condition evaluation.
    /// </summary>
    [JsonProperty("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new();
}

/// <summary>
/// Information about the action being performed.
/// </summary>
public class ActionInfo
{
    /// <summary>
    /// The action name (e.g., Microsoft.Storage/storageAccounts/read).
    /// </summary>
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of action: Action or DataAction.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; set; } = ActionType.Action;
}

public static class ActionType
{
    public const string Action = "Action";
    public const string DataAction = "DataAction";
}

/// <summary>
/// Batch check access request.
/// </summary>
public class BatchCheckAccessRequest
{
    /// <summary>
    /// Individual check access requests.
    /// </summary>
    [JsonProperty("requests")]
    public List<BatchCheckAccessItem> Requests { get; set; } = new();
}

/// <summary>
/// Individual item in a batch check access request.
/// </summary>
public class BatchCheckAccessItem
{
    /// <summary>
    /// Request ID for correlation.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The subject requesting access.
    /// </summary>
    [JsonProperty("subject")]
    public SubjectInfo Subject { get; set; } = new();

    /// <summary>
    /// The resource being accessed.
    /// </summary>
    [JsonProperty("resource")]
    public ResourceInfo Resource { get; set; } = new();

    /// <summary>
    /// The action being performed.
    /// </summary>
    [JsonProperty("action")]
    public ActionInfo Action { get; set; } = new();
}

using Newtonsoft.Json;

namespace Rbac.Shared.Models.Common;

/// <summary>
/// Represents a set of permissions with allow/deny patterns.
/// </summary>
public class Permission
{
    /// <summary>
    /// Allowed control plane operations (e.g., Microsoft.Storage/storageAccounts/read).
    /// </summary>
    [JsonProperty("actions")]
    public List<string> Actions { get; set; } = new();

    /// <summary>
    /// Excluded control plane operations.
    /// </summary>
    [JsonProperty("notActions")]
    public List<string> NotActions { get; set; } = new();

    /// <summary>
    /// Allowed data plane operations.
    /// </summary>
    [JsonProperty("dataActions")]
    public List<string> DataActions { get; set; } = new();

    /// <summary>
    /// Excluded data plane operations.
    /// </summary>
    [JsonProperty("notDataActions")]
    public List<string> NotDataActions { get; set; } = new();
}

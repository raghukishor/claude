using Newtonsoft.Json;

namespace Rbac.Shared.Models.Common;

/// <summary>
/// Represents an attribute-based access control condition.
/// </summary>
public class Condition
{
    /// <summary>
    /// The condition expression.
    /// Example: @Resource[Microsoft.Storage/storageAccounts/blobServices/containers:name] StringEquals 'public'
    /// </summary>
    [JsonProperty("expression")]
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Condition language version.
    /// </summary>
    [JsonProperty("version")]
    public string Version { get; set; } = "2.0";
}

using Newtonsoft.Json;

namespace Rbac.Shared.Models.Common;

/// <summary>
/// Tracks audit metadata for documents.
/// </summary>
public class MetadataInfo
{
    /// <summary>
    /// Principal who created the document.
    /// </summary>
    [JsonProperty("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when document was created.
    /// </summary>
    [JsonProperty("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Principal who last updated the document.
    /// </summary>
    [JsonProperty("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when document was last updated.
    /// </summary>
    [JsonProperty("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }
}

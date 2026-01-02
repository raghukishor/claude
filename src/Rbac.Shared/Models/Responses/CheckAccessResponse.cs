using Newtonsoft.Json;

namespace Rbac.Shared.Models.Responses;

/// <summary>
/// Response from a check access request.
/// </summary>
public class CheckAccessResponse
{
    /// <summary>
    /// The authorization decision: Allow or Deny.
    /// </summary>
    [JsonProperty("decision")]
    public string Decision { get; set; } = AccessDecision.Deny;

    /// <summary>
    /// Timestamp when the decision was made.
    /// </summary>
    [JsonProperty("decidedAt")]
    public DateTimeOffset DecidedAt { get; set; }

    /// <summary>
    /// Reason for the decision.
    /// </summary>
    [JsonProperty("reason")]
    public DecisionReason Reason { get; set; } = new();

    /// <summary>
    /// Role assignments that matched the request.
    /// </summary>
    [JsonProperty("matchedAssignments")]
    public List<MatchedAssignment> MatchedAssignments { get; set; } = new();

    /// <summary>
    /// Details about the evaluation process.
    /// </summary>
    [JsonProperty("evaluationDetails")]
    public EvaluationDetails EvaluationDetails { get; set; } = new();
}

/// <summary>
/// Reason for the authorization decision.
/// </summary>
public class DecisionReason
{
    /// <summary>
    /// Reason code.
    /// </summary>
    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable message.
    /// </summary>
    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Information about a role assignment that matched the request.
/// </summary>
public class MatchedAssignment
{
    /// <summary>
    /// The role assignment ID.
    /// </summary>
    [JsonProperty("assignmentId")]
    public string AssignmentId { get; set; } = string.Empty;

    /// <summary>
    /// The role definition ID.
    /// </summary>
    [JsonProperty("roleDefinitionId")]
    public string RoleDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// The role name.
    /// </summary>
    [JsonProperty("roleName")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// The scope where the assignment was made.
    /// </summary>
    [JsonProperty("scope")]
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// The permission pattern that matched.
    /// </summary>
    [JsonProperty("matchedPermission")]
    public string MatchedPermission { get; set; } = string.Empty;
}

/// <summary>
/// Details about the evaluation process.
/// </summary>
public class EvaluationDetails
{
    /// <summary>
    /// Whether the principal was resolved.
    /// </summary>
    [JsonProperty("principalResolved")]
    public bool PrincipalResolved { get; set; }

    /// <summary>
    /// Number of groups evaluated.
    /// </summary>
    [JsonProperty("groupsEvaluated")]
    public int GroupsEvaluated { get; set; }

    /// <summary>
    /// Number of roles evaluated.
    /// </summary>
    [JsonProperty("rolesEvaluated")]
    public int RolesEvaluated { get; set; }

    /// <summary>
    /// Number of conditions evaluated.
    /// </summary>
    [JsonProperty("conditionsEvaluated")]
    public int ConditionsEvaluated { get; set; }

    /// <summary>
    /// Total evaluation time in milliseconds.
    /// </summary>
    [JsonProperty("durationMs")]
    public long DurationMs { get; set; }
}

public static class AccessDecision
{
    public const string Allow = "Allow";
    public const string Deny = "Deny";
}

public static class ReasonCode
{
    public const string RoleAssignmentMatch = "RoleAssignmentMatch";
    public const string NoMatchingRoleAssignment = "NoMatchingRoleAssignment";
    public const string ActionExcluded = "ActionExcluded";
    public const string ConditionNotSatisfied = "ConditionNotSatisfied";
    public const string PrincipalNotFound = "PrincipalNotFound";
    public const string InvalidRequest = "InvalidRequest";
}

/// <summary>
/// Response from a batch check access request.
/// </summary>
public class BatchCheckAccessResponse
{
    /// <summary>
    /// Individual responses.
    /// </summary>
    [JsonProperty("responses")]
    public List<BatchCheckAccessItemResponse> Responses { get; set; } = new();

    /// <summary>
    /// Batch-level metadata.
    /// </summary>
    [JsonProperty("batchMetadata")]
    public BatchMetadata BatchMetadata { get; set; } = new();
}

/// <summary>
/// Individual response in a batch check access.
/// </summary>
public class BatchCheckAccessItemResponse
{
    /// <summary>
    /// Request ID for correlation.
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The authorization decision.
    /// </summary>
    [JsonProperty("decision")]
    public string Decision { get; set; } = AccessDecision.Deny;

    /// <summary>
    /// Reason for the decision.
    /// </summary>
    [JsonProperty("reason")]
    public DecisionReason Reason { get; set; } = new();

    /// <summary>
    /// Matched assignments.
    /// </summary>
    [JsonProperty("matchedAssignments")]
    public List<MatchedAssignment> MatchedAssignments { get; set; } = new();
}

/// <summary>
/// Batch-level metadata.
/// </summary>
public class BatchMetadata
{
    /// <summary>
    /// Total number of requests.
    /// </summary>
    [JsonProperty("totalRequests")]
    public int TotalRequests { get; set; }

    /// <summary>
    /// Number of allowed requests.
    /// </summary>
    [JsonProperty("allowed")]
    public int Allowed { get; set; }

    /// <summary>
    /// Number of denied requests.
    /// </summary>
    [JsonProperty("denied")]
    public int Denied { get; set; }

    /// <summary>
    /// Total processing time in milliseconds.
    /// </summary>
    [JsonProperty("totalDurationMs")]
    public long TotalDurationMs { get; set; }
}

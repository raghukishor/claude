using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.DataPlane;
using Rbac.Shared.Utilities;

namespace Rbac.DataPlane.Services;

public class PermissionEvaluator
{
    /// <summary>
    /// Evaluates if an action is allowed based on the effective role's permissions.
    /// </summary>
    public PermissionEvaluationResult Evaluate(string action, EffectiveRole role)
    {
        var permission = role.Permissions;

        // Check if action is explicitly denied by notActions
        var notActionsMatch = WildcardMatcher.MatchesAny(action, permission.NotActions);
        if (notActionsMatch != null)
        {
            return new PermissionEvaluationResult
            {
                IsAllowed = false,
                Reason = $"Action denied by notActions pattern: {notActionsMatch}",
                MatchedPattern = null
            };
        }

        // Check if action is allowed by actions
        var actionsMatch = WildcardMatcher.MatchesAny(action, permission.Actions);
        if (actionsMatch != null)
        {
            return new PermissionEvaluationResult
            {
                IsAllowed = true,
                Reason = $"Action allowed by actions pattern: {actionsMatch}",
                MatchedPattern = actionsMatch
            };
        }

        return new PermissionEvaluationResult
        {
            IsAllowed = false,
            Reason = "No matching permission found",
            MatchedPattern = null
        };
    }

    /// <summary>
    /// Evaluates if a data action is allowed based on the effective role's permissions.
    /// </summary>
    public PermissionEvaluationResult EvaluateDataAction(string dataAction, EffectiveRole role)
    {
        var permission = role.Permissions;

        // Check if data action is explicitly denied by notDataActions
        var notDataActionsMatch = WildcardMatcher.MatchesAny(dataAction, permission.NotDataActions);
        if (notDataActionsMatch != null)
        {
            return new PermissionEvaluationResult
            {
                IsAllowed = false,
                Reason = $"Data action denied by notDataActions pattern: {notDataActionsMatch}",
                MatchedPattern = null
            };
        }

        // Check if data action is allowed by dataActions
        var dataActionsMatch = WildcardMatcher.MatchesAny(dataAction, permission.DataActions);
        if (dataActionsMatch != null)
        {
            return new PermissionEvaluationResult
            {
                IsAllowed = true,
                Reason = $"Data action allowed by dataActions pattern: {dataActionsMatch}",
                MatchedPattern = dataActionsMatch
            };
        }

        return new PermissionEvaluationResult
        {
            IsAllowed = false,
            Reason = "No matching data permission found",
            MatchedPattern = null
        };
    }
}

public class PermissionEvaluationResult
{
    public bool IsAllowed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? MatchedPattern { get; set; }
}

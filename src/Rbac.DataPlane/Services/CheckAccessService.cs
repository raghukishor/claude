using System.Diagnostics;
using Rbac.DataPlane.Repositories;
using Rbac.Shared.Models.DataPlane;
using Rbac.Shared.Models.Requests;
using Rbac.Shared.Models.Responses;
using Rbac.Shared.Utilities;

namespace Rbac.DataPlane.Services;

public class CheckAccessService : ICheckAccessService
{
    private readonly IEffectiveAccessRepository _repository;
    private readonly PermissionEvaluator _permissionEvaluator;
    private readonly ConditionEvaluator _conditionEvaluator;

    public CheckAccessService(
        IEffectiveAccessRepository repository,
        PermissionEvaluator permissionEvaluator,
        ConditionEvaluator conditionEvaluator)
    {
        _repository = repository;
        _permissionEvaluator = permissionEvaluator;
        _conditionEvaluator = conditionEvaluator;
    }

    public async Task<CheckAccessResponse> CheckAccessAsync(CheckAccessRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var matchedAssignments = new List<MatchedAssignment>();
        var rolesEvaluated = 0;
        var conditionsEvaluated = 0;

        // Collect all principals (user + groups)
        var principals = new List<string> { request.Subject.PrincipalId };
        if (request.Subject.Groups != null)
        {
            principals.AddRange(request.Subject.Groups);
        }

        // Get scope hierarchy
        var scopeHierarchy = ScopeParser.GetScopeHierarchy(request.Resource.Scope);

        // Check each principal at each scope level
        foreach (var principalId in principals)
        {
            foreach (var scope in scopeHierarchy)
            {
                var effectiveAccess = await _repository.GetByPrincipalAndScopeAsync(principalId, scope);
                if (effectiveAccess == null)
                    continue;

                foreach (var role in effectiveAccess.EffectiveRoles)
                {
                    rolesEvaluated++;

                    // Evaluate permission
                    var isDataAction = request.Action.Type == ActionType.DataAction;
                    var permissionResult = isDataAction
                        ? _permissionEvaluator.EvaluateDataAction(request.Action.Name, role)
                        : _permissionEvaluator.Evaluate(request.Action.Name, role);

                    if (!permissionResult.IsAllowed)
                        continue;

                    // Evaluate condition if present
                    if (role.Condition != null && !string.IsNullOrEmpty(role.Condition.Expression))
                    {
                        conditionsEvaluated++;
                        var conditionResult = _conditionEvaluator.Evaluate(
                            role.Condition.Expression,
                            request.Resource.Attributes);

                        if (!conditionResult.IsSatisfied)
                            continue;
                    }

                    // Access granted!
                    matchedAssignments.Add(new MatchedAssignment
                    {
                        AssignmentId = role.AssignmentId,
                        RoleDefinitionId = role.RoleDefinitionId,
                        RoleName = role.RoleName,
                        Scope = scope,
                        MatchedPermission = permissionResult.MatchedPattern ?? ""
                    });
                }
            }
        }

        stopwatch.Stop();

        // Final decision: Allow if any role granted access
        var decision = matchedAssignments.Count > 0
            ? AccessDecision.Allow
            : AccessDecision.Deny;

        return new CheckAccessResponse
        {
            Decision = decision,
            DecidedAt = DateTimeOffset.UtcNow,
            Reason = new DecisionReason
            {
                Code = decision == AccessDecision.Allow
                    ? ReasonCode.RoleAssignmentMatch
                    : ReasonCode.NoMatchingRoleAssignment,
                Message = decision == AccessDecision.Allow
                    ? $"Access granted via {matchedAssignments.Count} role assignment(s)"
                    : "No matching role assignments found"
            },
            MatchedAssignments = matchedAssignments,
            EvaluationDetails = new EvaluationDetails
            {
                PrincipalResolved = true,
                GroupsEvaluated = request.Subject.Groups?.Count ?? 0,
                RolesEvaluated = rolesEvaluated,
                ConditionsEvaluated = conditionsEvaluated,
                DurationMs = stopwatch.ElapsedMilliseconds
            }
        };
    }

    public async Task<BatchCheckAccessResponse> BatchCheckAccessAsync(BatchCheckAccessRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var responses = new List<BatchCheckAccessItemResponse>();
        var allowed = 0;
        var denied = 0;

        foreach (var item in request.Requests)
        {
            var checkRequest = new CheckAccessRequest
            {
                Subject = item.Subject,
                Resource = item.Resource,
                Action = item.Action
            };

            var result = await CheckAccessAsync(checkRequest);

            if (result.Decision == AccessDecision.Allow)
                allowed++;
            else
                denied++;

            responses.Add(new BatchCheckAccessItemResponse
            {
                Id = item.Id,
                Decision = result.Decision,
                Reason = result.Reason,
                MatchedAssignments = result.MatchedAssignments
            });
        }

        stopwatch.Stop();

        return new BatchCheckAccessResponse
        {
            Responses = responses,
            BatchMetadata = new BatchMetadata
            {
                TotalRequests = request.Requests.Count,
                Allowed = allowed,
                Denied = denied,
                TotalDurationMs = stopwatch.ElapsedMilliseconds
            }
        };
    }
}

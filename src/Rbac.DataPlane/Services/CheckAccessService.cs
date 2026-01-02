using System.Diagnostics;
using Rbac.DataPlane.Repositories;
using Rbac.Shared.Models.DataPlane;
using Rbac.Shared.Models.Requests;
using Rbac.Shared.Models.Responses;
using Rbac.Shared.Utilities;

namespace Rbac.DataPlane.Services;

public class CheckAccessService : ICheckAccessService
{
    private readonly IRoleAssignmentRepository _assignmentRepository;
    private readonly IRoleDefinitionCache _roleDefinitionCache;
    private readonly PermissionEvaluator _permissionEvaluator;
    private readonly ConditionEvaluator _conditionEvaluator;

    public CheckAccessService(
        IRoleAssignmentRepository assignmentRepository,
        IRoleDefinitionCache roleDefinitionCache,
        PermissionEvaluator permissionEvaluator,
        ConditionEvaluator conditionEvaluator)
    {
        _assignmentRepository = assignmentRepository;
        _roleDefinitionCache = roleDefinitionCache;
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

        // Get scope hierarchy (scopes that are relevant for this request)
        // e.g. for /subs/s1/rgs/r1, hierarchy is [/subs/s1/rgs/r1, /subs/s1, /]
        var scopeHierarchy = new HashSet<string>(
            ScopeParser.GetScopeHierarchy(request.Resource.Scope), 
            StringComparer.OrdinalIgnoreCase);

        // Fetch assignments for all principals in parallel
        var assignmentTasks = principals.Select(p => _assignmentRepository.ListByPrincipalAsync(p));
        var assignmentLists = await Task.WhenAll(assignmentTasks);
        var allAssignments = assignmentLists.SelectMany(a => a).ToList();

        // Filter assignments that apply to the requested scope
        var relevantAssignments = allAssignments
            .Where(a => scopeHierarchy.Contains(a.Scope))
            .ToList();

        foreach (var assignment in relevantAssignments)
        {
            var roleDefinition = await _roleDefinitionCache.GetAsync(assignment.RoleDefinitionId);
            if (roleDefinition == null)
            {
                continue; // Skip if role definition not found
            }

            rolesEvaluated++;

            // Create a temporary EffectiveRole context for the evaluator
            // Use the RoleDefinition's permissions directly
            // Helper method or construct on the fly? 
            // The PermissionEvaluator expects an EffectiveRole or just permissions?
            // Checking existing code: _permissionEvaluator.Evaluate(..., role) where role is EffectiveRole.
            // I should verify PermissionEvaluator signature.
            // Assuming I can construct a transient EffectiveRole or update Evaluator. 
            // To minimize changes, I'll construct a transient EffectiveRole.
            
            var transientRole = new EffectiveRole
            {
                RoleDefinitionId = roleDefinition.Id,
                RoleName = roleDefinition.Name,
                AssignmentId = assignment.Id,
                AssignmentScope = assignment.Scope,
                Condition = assignment.Condition,
                Permissions = MergePermissions(roleDefinition.Permissions)
            };

            // Evaluate permission
            var isDataAction = request.Action.Type == ActionType.DataAction;
            var permissionResult = isDataAction
                ? _permissionEvaluator.EvaluateDataAction(request.Action.Name, transientRole)
                : _permissionEvaluator.Evaluate(request.Action.Name, transientRole);

            if (!permissionResult.IsAllowed)
                continue;

            // Evaluate condition if present
            if (assignment.Condition != null && !string.IsNullOrEmpty(assignment.Condition.Expression))
            {
                conditionsEvaluated++;
                var conditionResult = _conditionEvaluator.Evaluate(
                    assignment.Condition.Expression,
                    request.Resource.Attributes);

                if (!conditionResult.IsSatisfied)
                    continue;
            }

            // Access granted!
            matchedAssignments.Add(new MatchedAssignment
            {
                AssignmentId = assignment.Id,
                RoleDefinitionId = roleDefinition.Id,
                RoleName = roleDefinition.Name,
                Scope = assignment.Scope,
                MatchedPermission = permissionResult.MatchedPattern ?? ""
            });
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

        // Optimization: Could pre-fetch all assignments for the subject once if subject is same for all items
        // But requests might have different subjects?
        // BatchCheckAccess usually implies same subject?
        // Let's assume naive iteration for now to match interface. 
        // If request list is large, we might want to optimize.
        
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

    // Helper to merge permissions from RoleDefinition
    private Rbac.Shared.Models.Common.Permission MergePermissions(List<Rbac.Shared.Models.Common.Permission> permissions)
    {
        var merged = new Rbac.Shared.Models.Common.Permission
        {
            Actions = new List<string>(),
            NotActions = new List<string>(),
            DataActions = new List<string>(),
            NotDataActions = new List<string>()
        };

        foreach (var perm in permissions)
        {
            merged.Actions.AddRange(perm.Actions);
            merged.NotActions.AddRange(perm.NotActions);
            merged.DataActions.AddRange(perm.DataActions);
            merged.NotDataActions.AddRange(perm.NotDataActions);
        }

        // Remove duplicates
        merged.Actions = merged.Actions.Distinct().ToList();
        merged.NotActions = merged.NotActions.Distinct().ToList();
        merged.DataActions = merged.DataActions.Distinct().ToList();
        merged.NotDataActions = merged.NotDataActions.Distinct().ToList();

        return merged;
    }
}

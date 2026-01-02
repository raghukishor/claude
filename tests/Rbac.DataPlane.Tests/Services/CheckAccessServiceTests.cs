using FluentAssertions;
using Moq;
using Rbac.DataPlane.Repositories;
using Rbac.DataPlane.Services;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.DataPlane;
using Rbac.Shared.Models.Requests;
using Rbac.Shared.Models.Responses;

namespace Rbac.DataPlane.Tests.Services;

public class CheckAccessServiceTests
{
    private readonly Mock<IEffectiveAccessRepository> _repositoryMock;
    private readonly PermissionEvaluator _permissionEvaluator;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly CheckAccessService _service;

    public CheckAccessServiceTests()
    {
        _repositoryMock = new Mock<IEffectiveAccessRepository>();
        _permissionEvaluator = new PermissionEvaluator();
        _conditionEvaluator = new ConditionEvaluator();
        _service = new CheckAccessService(
            _repositoryMock.Object,
            _permissionEvaluator,
            _conditionEvaluator);
    }

    [Fact]
    public async Task CheckAccessAsync_WithMatchingPermission_ShouldAllow()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");
        var effectiveAccess = CreateEffectiveAccess("user-001", "/subscriptions/sub-001", new[] { "Microsoft.Storage/*" });

        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/subscriptions/sub-001"))
            .ReturnsAsync(effectiveAccess);
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/"))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Allow);
        result.MatchedAssignments.Should().HaveCount(1);
        result.Reason.Code.Should().Be(ReasonCode.RoleAssignmentMatch);
    }

    [Fact]
    public async Task CheckAccessAsync_WithNoMatchingPermission_ShouldDeny()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/delete");
        var effectiveAccess = CreateEffectiveAccess("user-001", "/subscriptions/sub-001", new[] { "Microsoft.Storage/storageAccounts/read" });

        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/subscriptions/sub-001"))
            .ReturnsAsync(effectiveAccess);
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/"))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Deny);
        result.Reason.Code.Should().Be(ReasonCode.NoMatchingRoleAssignment);
    }

    [Fact]
    public async Task CheckAccessAsync_WithNoEffectiveAccess_ShouldDeny()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");

        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Deny);
    }

    [Fact]
    public async Task CheckAccessAsync_WithInheritedPermission_ShouldAllow()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001/resourceGroups/rg-001", "Microsoft.Storage/storageAccounts/read");

        // No access at resource group level
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/subscriptions/sub-001/resourceGroups/rg-001"))
            .ReturnsAsync((EffectiveAccess?)null);

        // But has access at subscription level
        var effectiveAccess = CreateEffectiveAccess("user-001", "/subscriptions/sub-001", new[] { "Microsoft.Storage/*" });
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/subscriptions/sub-001"))
            .ReturnsAsync(effectiveAccess);

        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/"))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Allow);
    }

    [Fact]
    public async Task CheckAccessAsync_WithGroupMembership_ShouldAllow()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");
        request.Subject.Groups = new List<string> { "group-admins" };

        // No access for user directly
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", It.IsAny<string>()))
            .ReturnsAsync((EffectiveAccess?)null);

        // But group has access
        var groupAccess = CreateEffectiveAccess("group-admins", "/subscriptions/sub-001", new[] { "Microsoft.Storage/*" });
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("group-admins", "/subscriptions/sub-001"))
            .ReturnsAsync(groupAccess);
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("group-admins", "/"))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Allow);
    }

    [Fact]
    public async Task CheckAccessAsync_WithCondition_Satisfied_ShouldAllow()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");
        request.Resource.Attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "prod" }
        };

        var effectiveAccess = CreateEffectiveAccess("user-001", "/subscriptions/sub-001", new[] { "Microsoft.Storage/*" });
        effectiveAccess.EffectiveRoles[0].Condition = new Condition
        {
            Expression = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'"
        };

        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/subscriptions/sub-001"))
            .ReturnsAsync(effectiveAccess);
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/"))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Allow);
    }

    [Fact]
    public async Task CheckAccessAsync_WithCondition_NotSatisfied_ShouldDeny()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");
        request.Resource.Attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "dev" }
        };

        var effectiveAccess = CreateEffectiveAccess("user-001", "/subscriptions/sub-001", new[] { "Microsoft.Storage/*" });
        effectiveAccess.EffectiveRoles[0].Condition = new Condition
        {
            Expression = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'"
        };

        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/subscriptions/sub-001"))
            .ReturnsAsync(effectiveAccess);
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/"))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Deny);
    }

    [Fact]
    public async Task BatchCheckAccessAsync_ShouldProcessAllRequests()
    {
        var request = new BatchCheckAccessRequest
        {
            Requests = new List<BatchCheckAccessItem>
            {
                new() { Id = "1", Subject = new SubjectInfo { PrincipalId = "user-001" }, Resource = new ResourceInfo { Scope = "/subscriptions/sub-001" }, Action = new ActionInfo { Name = "Microsoft.Storage/storageAccounts/read" } },
                new() { Id = "2", Subject = new SubjectInfo { PrincipalId = "user-002" }, Resource = new ResourceInfo { Scope = "/subscriptions/sub-001" }, Action = new ActionInfo { Name = "Microsoft.Storage/storageAccounts/read" } }
            }
        };

        var effectiveAccess1 = CreateEffectiveAccess("user-001", "/subscriptions/sub-001", new[] { "Microsoft.Storage/*" });
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/subscriptions/sub-001"))
            .ReturnsAsync(effectiveAccess1);
        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-001", "/"))
            .ReturnsAsync((EffectiveAccess?)null);

        _repositoryMock.Setup(r => r.GetByPrincipalAndScopeAsync("user-002", It.IsAny<string>()))
            .ReturnsAsync((EffectiveAccess?)null);

        var result = await _service.BatchCheckAccessAsync(request);

        result.Responses.Should().HaveCount(2);
        result.Responses[0].Decision.Should().Be(AccessDecision.Allow);
        result.Responses[1].Decision.Should().Be(AccessDecision.Deny);
        result.BatchMetadata.TotalRequests.Should().Be(2);
        result.BatchMetadata.Allowed.Should().Be(1);
        result.BatchMetadata.Denied.Should().Be(1);
    }

    private static CheckAccessRequest CreateRequest(string principalId, string scope, string action)
    {
        return new CheckAccessRequest
        {
            Subject = new SubjectInfo { PrincipalId = principalId },
            Resource = new ResourceInfo { Scope = scope },
            Action = new ActionInfo { Name = action }
        };
    }

    private static EffectiveAccess CreateEffectiveAccess(string principalId, string scope, string[] actions)
    {
        return new EffectiveAccess
        {
            Id = $"ea-{principalId}-{scope.GetHashCode():x}",
            PrincipalId = principalId,
            Scope = scope,
            EffectiveRoles = new List<EffectiveRole>
            {
                new()
                {
                    RoleDefinitionId = "rd-test",
                    RoleName = "Test Role",
                    AssignmentId = "ra-test",
                    AssignmentScope = scope,
                    Permissions = new Permission
                    {
                        Actions = actions.ToList()
                    }
                }
            }
        };
    }
}

using FluentAssertions;
using Moq;
using Rbac.DataPlane.Repositories;
using Rbac.DataPlane.Services;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.Requests;
using Rbac.Shared.Models.Responses;

namespace Rbac.DataPlane.Tests.Services;

public class CheckAccessServiceTests
{
    private readonly Mock<IRoleAssignmentRepository> _assignmentRepositoryMock;
    private readonly Mock<IRoleDefinitionCache> _roleDefinitionCacheMock;
    private readonly PermissionEvaluator _permissionEvaluator;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly CheckAccessService _service;

    public CheckAccessServiceTests()
    {
        _assignmentRepositoryMock = new Mock<IRoleAssignmentRepository>();
        _roleDefinitionCacheMock = new Mock<IRoleDefinitionCache>();
        _permissionEvaluator = new PermissionEvaluator();
        _conditionEvaluator = new ConditionEvaluator();
        _service = new CheckAccessService(
            _assignmentRepositoryMock.Object,
            _roleDefinitionCacheMock.Object,
            _permissionEvaluator,
            _conditionEvaluator);
    }

    [Fact]
    public async Task CheckAccessAsync_WithMatchingPermission_ShouldAllow()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");
        var assignment = CreateAssignment("user-001", "rd-storage-blob", "/subscriptions/sub-001");
        var roleDef = CreateRoleDefinition("rd-storage-blob", new[] { "Microsoft.Storage/*" });

        SetupAssignments("user-001", new[] { assignment });
        SetupRoleDefinition("rd-storage-blob", roleDef);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Allow);
        result.MatchedAssignments.Should().HaveCount(1);
        result.Reason.Code.Should().Be(ReasonCode.RoleAssignmentMatch);
    }

    [Fact]
    public async Task CheckAccessAsync_WithNoMatchingPermission_ShouldDeny()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/delete");
        var assignment = CreateAssignment("user-001", "rd-storage-reader", "/subscriptions/sub-001");
        var roleDef = CreateRoleDefinition("rd-storage-reader", new[] { "Microsoft.Storage/*/read" });

        SetupAssignments("user-001", new[] { assignment });
        SetupRoleDefinition("rd-storage-reader", roleDef);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Deny);
        result.Reason.Code.Should().Be(ReasonCode.NoMatchingRoleAssignment);
    }

    [Fact]
    public async Task CheckAccessAsync_WithNoAssignments_ShouldDeny()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");

        SetupAssignments("user-001", Array.Empty<RoleAssignment>());

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Deny);
    }

    [Fact]
    public async Task CheckAccessAsync_WithInheritedPermission_ShouldAllow()
    {
        // Request access at RG level
        var request = CreateRequest("user-001", "/subscriptions/sub-001/resourceGroups/rg-001", "Microsoft.Storage/storageAccounts/read");
        
        // Assignment is at Subscription level
        var assignment = CreateAssignment("user-001", "rd-storage-contrib", "/subscriptions/sub-001");
        var roleDef = CreateRoleDefinition("rd-storage-contrib", new[] { "Microsoft.Storage/*" });

        SetupAssignments("user-001", new[] { assignment });
        SetupRoleDefinition("rd-storage-contrib", roleDef);

        var result = await _service.CheckAccessAsync(request);

        result.Decision.Should().Be(AccessDecision.Allow);
        result.MatchedAssignments.Should().Contain(a => a.Scope == "/subscriptions/sub-001");
    }

    [Fact]
    public async Task CheckAccessAsync_WithGroupMembership_ShouldAllow()
    {
        var request = CreateRequest("user-001", "/subscriptions/sub-001", "Microsoft.Storage/storageAccounts/read");
        request.Subject.Groups = new List<string> { "group-admins" };

        // User has no direct assignments
        SetupAssignments("user-001", Array.Empty<RoleAssignment>());

        // Group has assignment
        var groupAssignment = CreateAssignment("group-admins", "rd-owner", "/subscriptions/sub-001");
        var roleDef = CreateRoleDefinition("rd-owner", new[] { "*" });

        SetupAssignments("group-admins", new[] { groupAssignment });
        SetupRoleDefinition("rd-owner", roleDef);

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

        var assignment = CreateAssignment("user-001", "rd-storage-params", "/subscriptions/sub-001");
        assignment.Condition = new Condition
        {
            Expression = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'"
        };

        var roleDef = CreateRoleDefinition("rd-storage-params", new[] { "Microsoft.Storage/*" });

        SetupAssignments("user-001", new[] { assignment });
        SetupRoleDefinition("rd-storage-params", roleDef);

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

        var assignment = CreateAssignment("user-001", "rd-storage-params", "/subscriptions/sub-001");
        assignment.Condition = new Condition
        {
            Expression = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'"
        };

        var roleDef = CreateRoleDefinition("rd-storage-params", new[] { "Microsoft.Storage/*" });

        SetupAssignments("user-001", new[] { assignment });
        SetupRoleDefinition("rd-storage-params", roleDef);

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

        // User 1 has access
        var assignment1 = CreateAssignment("user-001", "rd-access", "/subscriptions/sub-001");
        SetupAssignments("user-001", new[] { assignment1 });
        
        // User 2 has NO access
        SetupAssignments("user-002", Array.Empty<RoleAssignment>());

        // Role definition
        var roleDef = CreateRoleDefinition("rd-access", new[] { "Microsoft.Storage/storageAccounts/read" });
        SetupRoleDefinition("rd-access", roleDef);

        var result = await _service.BatchCheckAccessAsync(request);

        result.Responses.Should().HaveCount(2);
        result.Responses[0].Decision.Should().Be(AccessDecision.Allow);
        result.Responses[1].Decision.Should().Be(AccessDecision.Deny);
        result.BatchMetadata.Allowed.Should().Be(1);
        result.BatchMetadata.Denied.Should().Be(1);
    }

    private void SetupAssignments(string principalId, IEnumerable<RoleAssignment> assignments)
    {
        _assignmentRepositoryMock.Setup(r => r.ListByPrincipalAsync(principalId))
            .ReturnsAsync(assignments.ToList());
    }

    private void SetupRoleDefinition(string id, RoleDefinition roleDef)
    {
        _roleDefinitionCacheMock.Setup(c => c.GetAsync(id))
            .ReturnsAsync(roleDef);
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

    private static RoleAssignment CreateAssignment(string principalId, string roleDefId, string scope)
    {
        return new RoleAssignment
        {
            Id = $"ra-{Guid.NewGuid()}",
            PrincipalId = principalId,
            RoleDefinitionId = roleDefId,
            Scope = scope,
            PrincipalType = "User"
        };
    }

    private static RoleDefinition CreateRoleDefinition(string id, string[] actions)
    {
        return new RoleDefinition
        {
            Id = id,
            Name = "Test Role",
            Permissions = new List<Permission>
            {
                new() { Actions = actions.ToList() }
            }
        };
    }
}

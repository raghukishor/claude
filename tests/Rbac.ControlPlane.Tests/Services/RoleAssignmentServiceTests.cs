using FluentAssertions;
using Moq;
using Rbac.ControlPlane.Repositories;
using Rbac.ControlPlane.Services;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.Requests;

namespace Rbac.ControlPlane.Tests.Services;

public class RoleAssignmentServiceTests
{
    private readonly Mock<IRoleAssignmentRepository> _assignmentRepositoryMock;
    private readonly Mock<IRoleDefinitionRepository> _definitionRepositoryMock;
    private readonly RoleAssignmentService _service;

    public RoleAssignmentServiceTests()
    {
        _assignmentRepositoryMock = new Mock<IRoleAssignmentRepository>();
        _definitionRepositoryMock = new Mock<IRoleDefinitionRepository>();
        _service = new RoleAssignmentService(
            _assignmentRepositoryMock.Object,
            _definitionRepositoryMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingAssignment_ShouldReturnAssignment()
    {
        var assignment = CreateTestRoleAssignment();
        _assignmentRepositoryMock.Setup(r => r.GetByIdAsync(assignment.Id, assignment.Scope))
            .ReturnsAsync(assignment);

        var result = await _service.GetByIdAsync(assignment.Id, assignment.Scope);

        result.Should().NotBeNull();
        result!.Id.Should().Be(assignment.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingAssignment_ShouldReturnNull()
    {
        _assignmentRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((RoleAssignment?)null);

        var result = await _service.GetByIdAsync("non-existing", "/subscriptions/sub-001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ShouldReturnAssignmentsForScope()
    {
        var scope = "/subscriptions/sub-001";
        var assignments = new List<RoleAssignment>
        {
            CreateTestRoleAssignment("ra-001", scope),
            CreateTestRoleAssignment("ra-002", scope)
        };
        _assignmentRepositoryMock.Setup(r => r.ListAsync(scope, false))
            .ReturnsAsync(assignments);

        var result = await _service.ListAsync(scope);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListByPrincipalAsync_ShouldReturnAssignmentsForPrincipal()
    {
        var principalId = "user-001";
        var assignments = new List<RoleAssignment>
        {
            CreateTestRoleAssignment("ra-001", "/subscriptions/sub-001", principalId),
            CreateTestRoleAssignment("ra-002", "/subscriptions/sub-002", principalId)
        };
        _assignmentRepositoryMock.Setup(r => r.ListByPrincipalAsync(principalId, false))
            .ReturnsAsync(assignments);

        var result = await _service.ListByPrincipalAsync(principalId);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.PrincipalId == principalId);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateAssignment()
    {
        var scope = "/subscriptions/sub-001";
        var roleDefinition = CreateTestRoleDefinition();

        var request = new CreateRoleAssignmentRequest
        {
            PrincipalId = "user-001",
            PrincipalType = "User",
            RoleDefinitionId = roleDefinition.Id
        };

        _definitionRepositoryMock.Setup(r => r.GetByIdAsync(roleDefinition.Id))
            .ReturnsAsync(roleDefinition);
        _assignmentRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<RoleAssignment>()))
            .ReturnsAsync((RoleAssignment a) => a);

        var result = await _service.CreateAsync(scope, request);

        result.Should().NotBeNull();
        result.Id.Should().StartWith("ra-");
        result.Scope.Should().Be(scope);
        result.PrincipalId.Should().Be("user-001");
        result.RoleDefinitionId.Should().Be(roleDefinition.Id);
        result.Version.SequenceNumber.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidScope_ShouldThrowArgumentException()
    {
        var request = new CreateRoleAssignmentRequest
        {
            PrincipalId = "user-001",
            RoleDefinitionId = "rd-001"
        };

        var action = () => _service.CreateAsync("invalid-scope", request);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid scope*");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyPrincipalId_ShouldThrowArgumentException()
    {
        var request = new CreateRoleAssignmentRequest
        {
            PrincipalId = "",
            RoleDefinitionId = "rd-001"
        };

        var action = () => _service.CreateAsync("/subscriptions/sub-001", request);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*PrincipalId*required*");
    }

    [Fact]
    public async Task CreateAsync_WithNonExistingRoleDefinition_ShouldThrowKeyNotFoundException()
    {
        var request = new CreateRoleAssignmentRequest
        {
            PrincipalId = "user-001",
            RoleDefinitionId = "rd-non-existing"
        };

        _definitionRepositoryMock.Setup(r => r.GetByIdAsync("rd-non-existing"))
            .ReturnsAsync((RoleDefinition?)null);

        var action = () => _service.CreateAsync("/subscriptions/sub-001", request);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Role definition*not found*");
    }

    [Fact]
    public async Task CreateAsync_WithScopeOutsideAssignableScopes_ShouldThrowArgumentException()
    {
        var roleDefinition = new RoleDefinition
        {
            Id = "rd-001",
            Name = "Limited Role",
            AssignableScopes = new List<string> { "/subscriptions/sub-other" }
        };

        var request = new CreateRoleAssignmentRequest
        {
            PrincipalId = "user-001",
            RoleDefinitionId = "rd-001"
        };

        _definitionRepositoryMock.Setup(r => r.GetByIdAsync("rd-001"))
            .ReturnsAsync(roleDefinition);

        var action = () => _service.CreateAsync("/subscriptions/sub-001", request);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not within the assignable scopes*");
    }

    [Fact]
    public async Task CreateAsync_WithCondition_ShouldIncludeCondition()
    {
        var scope = "/subscriptions/sub-001";
        var roleDefinition = CreateTestRoleDefinition();

        var request = new CreateRoleAssignmentRequest
        {
            PrincipalId = "user-001",
            PrincipalType = "User",
            RoleDefinitionId = roleDefinition.Id,
            Condition = new ConditionRequest
            {
                Expression = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'",
                Version = "2.0"
            }
        };

        _definitionRepositoryMock.Setup(r => r.GetByIdAsync(roleDefinition.Id))
            .ReturnsAsync(roleDefinition);
        _assignmentRepositoryMock.Setup(r => r.CreateAsync(It.IsAny<RoleAssignment>()))
            .ReturnsAsync((RoleAssignment a) => a);

        var result = await _service.CreateAsync(scope, request);

        result.Condition.Should().NotBeNull();
        result.Condition!.Expression.Should().Contain("StringEquals");
        result.Condition.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallRepositoryDelete()
    {
        var id = "ra-001";
        var scope = "/subscriptions/sub-001";

        await _service.DeleteAsync(id, scope);

        _assignmentRepositoryMock.Verify(r => r.DeleteAsync(id, scope), Times.Once);
    }

    private static RoleAssignment CreateTestRoleAssignment(
        string id = "ra-test-001",
        string scope = "/subscriptions/sub-001",
        string principalId = "user-001")
    {
        return new RoleAssignment
        {
            Id = id,
            Scope = scope,
            PrincipalId = principalId,
            PrincipalType = "User",
            RoleDefinitionId = "rd-test-001",
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ChangeId = "chg-test-001"
            },
            Lifecycle = new LifecycleInfo
            {
                State = "Active",
                IsDeleted = false
            }
        };
    }

    private static RoleDefinition CreateTestRoleDefinition()
    {
        return new RoleDefinition
        {
            Id = "rd-test-001",
            Name = "Test Role",
            AssignableScopes = new List<string> { "/" },
            Permissions = new List<Permission>
            {
                new() { Actions = new List<string> { "Microsoft.Storage/*" } }
            }
        };
    }
}

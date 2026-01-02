using FluentAssertions;
using Moq;
using Rbac.ControlPlane.Repositories;
using Rbac.ControlPlane.Services;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.Requests;

namespace Rbac.ControlPlane.Tests.Services;

public class RoleDefinitionServiceTests
{
    private readonly Mock<IRoleDefinitionRepository> _repositoryMock;
    private readonly RoleDefinitionService _service;

    public RoleDefinitionServiceTests()
    {
        _repositoryMock = new Mock<IRoleDefinitionRepository>();
        _service = new RoleDefinitionService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingRole_ShouldReturnRole()
    {
        var role = CreateTestRoleDefinition();
        _repositoryMock.Setup(r => r.GetByIdAsync(role.Id))
            .ReturnsAsync(role);

        var result = await _service.GetByIdAsync(role.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(role.Id);
        result.Name.Should().Be(role.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingRole_ShouldReturnNull()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((RoleDefinition?)null);

        var result = await _service.GetByIdAsync("non-existing-id");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ShouldReturnAllRoles()
    {
        var roles = new List<RoleDefinition>
        {
            CreateTestRoleDefinition("rd-001", "Role 1"),
            CreateTestRoleDefinition("rd-002", "Role 2")
        };
        _repositoryMock.Setup(r => r.ListAsync(null, false))
            .ReturnsAsync(roles);

        var result = await _service.ListAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ShouldCreateRole()
    {
        var request = new CreateRoleDefinitionRequest
        {
            Name = "Test Role",
            Description = "Test Description",
            Permissions = new List<PermissionRequest>
            {
                new() { Actions = new List<string> { "Microsoft.Storage/*" } }
            },
            AssignableScopes = new List<string> { "/" }
        };

        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<RoleDefinition>()))
            .ReturnsAsync((RoleDefinition r) => r);

        var result = await _service.CreateAsync(request);

        result.Should().NotBeNull();
        result.Name.Should().Be("Test Role");
        result.Id.Should().StartWith("rd-");
        result.Version.Should().NotBeNull();
        result.Version.SequenceNumber.Should().Be(1);
        result.Version.ChangeId.Should().StartWith("chg-");
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ShouldThrowArgumentException()
    {
        var request = new CreateRoleDefinitionRequest
        {
            Name = "",
            Permissions = new List<PermissionRequest>
            {
                new() { Actions = new List<string> { "Microsoft.Storage/*" } }
            },
            AssignableScopes = new List<string> { "/" }
        };

        var action = () => _service.CreateAsync(request);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Name*required*");
    }

    [Fact]
    public async Task CreateAsync_WithNoPermissions_ShouldThrowArgumentException()
    {
        var request = new CreateRoleDefinitionRequest
        {
            Name = "Test Role",
            Permissions = new List<PermissionRequest>(),
            AssignableScopes = new List<string> { "/" }
        };

        var action = () => _service.CreateAsync(request);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*permission*required*");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidScope_ShouldThrowArgumentException()
    {
        var request = new CreateRoleDefinitionRequest
        {
            Name = "Test Role",
            Permissions = new List<PermissionRequest>
            {
                new() { Actions = new List<string> { "Microsoft.Storage/*" } }
            },
            AssignableScopes = new List<string> { "invalid-scope" }
        };

        var action = () => _service.CreateAsync(request);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid scope*");
    }

    [Fact]
    public async Task UpdateAsync_WithExistingRole_ShouldUpdateRole()
    {
        var existingRole = CreateTestRoleDefinition();
        _repositoryMock.Setup(r => r.GetByIdAsync(existingRole.Id))
            .ReturnsAsync(existingRole);
        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<RoleDefinition>(), null))
            .ReturnsAsync((RoleDefinition r, string? _) => r);

        var request = new UpdateRoleDefinitionRequest
        {
            Name = "Updated Name",
            Description = "Updated Description"
        };

        var result = await _service.UpdateAsync(existingRole.Id, request);

        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");
        result.Version.SequenceNumber.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistingRole_ShouldThrowKeyNotFoundException()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((RoleDefinition?)null);

        var request = new UpdateRoleDefinitionRequest { Name = "Updated" };

        var action = () => _service.UpdateAsync("non-existing-id", request);

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldCallRepositoryDelete()
    {
        var roleId = "rd-001";

        await _service.DeleteAsync(roleId);

        _repositoryMock.Verify(r => r.DeleteAsync(roleId), Times.Once);
    }

    private static RoleDefinition CreateTestRoleDefinition(
        string id = "rd-test-001",
        string name = "Test Role")
    {
        return new RoleDefinition
        {
            Id = id,
            Name = name,
            Description = "Test Description",
            Permissions = new List<Permission>
            {
                new()
                {
                    Actions = new List<string> { "Microsoft.Storage/*" }
                }
            },
            AssignableScopes = new List<string> { "/" },
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
}

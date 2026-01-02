using FluentAssertions;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.DataPlane;
using Rbac.SyncService.Processors;

namespace Rbac.SyncService.Tests.Processors;

public class DenormalizationEngineTests
{
    private readonly DenormalizationEngine _engine;

    public DenormalizationEngineTests()
    {
        _engine = new DenormalizationEngine();
    }

    [Fact]
    public void CreateEffectiveAccess_ShouldCreateDocumentWithCorrectProperties()
    {
        var assignment = CreateRoleAssignment();
        var definition = CreateRoleDefinition();

        var result = _engine.CreateEffectiveAccess(assignment, definition);

        result.Should().NotBeNull();
        result.Id.Should().StartWith("ea-");
        result.PrincipalId.Should().Be(assignment.PrincipalId);
        result.Scope.Should().Be(assignment.Scope);
        result.EffectiveRoles.Should().HaveCount(1);
        result.Sync.DocumentVersion.Should().Be(1);
        result.Sync.SyncMode.Should().Be(SyncModes.Incremental);
    }

    [Fact]
    public void CreateEffectiveRole_ShouldMergePermissions()
    {
        var assignment = CreateRoleAssignment();
        var definition = CreateRoleDefinition();
        definition.Permissions.Add(new Permission
        {
            Actions = new List<string> { "Microsoft.Compute/*" },
            DataActions = new List<string> { "Microsoft.Storage/*/read" }
        });

        var result = _engine.CreateEffectiveRole(assignment, definition);

        result.Permissions.Actions.Should().Contain("Microsoft.Storage/*");
        result.Permissions.Actions.Should().Contain("Microsoft.Compute/*");
        result.Permissions.DataActions.Should().Contain("Microsoft.Storage/*/read");
    }

    [Fact]
    public void CreateEffectiveRole_ShouldRemoveDuplicates()
    {
        var assignment = CreateRoleAssignment();
        var definition = CreateRoleDefinition();
        definition.Permissions.Add(new Permission
        {
            Actions = new List<string> { "Microsoft.Storage/*" } // Duplicate
        });

        var result = _engine.CreateEffectiveRole(assignment, definition);

        result.Permissions.Actions.Should().HaveCount(1); // No duplicates
    }

    [Fact]
    public void CreateEffectiveRole_ShouldPreserveCondition()
    {
        var assignment = CreateRoleAssignment();
        assignment.Condition = new Condition
        {
            Expression = "@Resource[name] StringEquals 'prod'",
            Version = "1.0"
        };
        var definition = CreateRoleDefinition();

        var result = _engine.CreateEffectiveRole(assignment, definition);

        result.Condition.Should().NotBeNull();
        result.Condition!.Expression.Should().Be("@Resource[name] StringEquals 'prod'");
    }

    [Fact]
    public void UpdateEffectiveAccess_ShouldAddNewRole()
    {
        var existing = CreateEffectiveAccess();
        var newAssignment = CreateRoleAssignment("ra-002");
        var definition = CreateRoleDefinition();

        var result = _engine.UpdateEffectiveAccess(existing, newAssignment, definition);

        result.EffectiveRoles.Should().HaveCount(2);
        result.Sync.DocumentVersion.Should().Be(2);
    }

    [Fact]
    public void UpdateEffectiveAccess_ShouldUpdateExistingRole()
    {
        var existing = CreateEffectiveAccess();
        var updatedAssignment = CreateRoleAssignment("ra-test-001");
        updatedAssignment.Version = new VersionInfo { SequenceNumber = 2, ChangeId = "chg-002" };
        var definition = CreateRoleDefinition();
        definition.Permissions[0].Actions = new List<string> { "Microsoft.Compute/*" };

        var result = _engine.UpdateEffectiveAccess(existing, updatedAssignment, definition);

        result.EffectiveRoles.Should().HaveCount(1);
        result.EffectiveRoles[0].Permissions.Actions.Should().Contain("Microsoft.Compute/*");
        result.EffectiveRoles[0].Permissions.Actions.Should().NotContain("Microsoft.Storage/*");
    }

    [Fact]
    public void RemoveRoleFromEffectiveAccess_WithMultipleRoles_ShouldRemoveOne()
    {
        var existing = CreateEffectiveAccess();
        existing.EffectiveRoles.Add(new EffectiveRole
        {
            RoleDefinitionId = "rd-002",
            RoleName = "Role 2",
            AssignmentId = "ra-002"
        });

        var result = _engine.RemoveRoleFromEffectiveAccess(existing, "ra-test-001");

        result.Should().NotBeNull();
        result!.EffectiveRoles.Should().HaveCount(1);
        result.EffectiveRoles[0].AssignmentId.Should().Be("ra-002");
    }

    [Fact]
    public void RemoveRoleFromEffectiveAccess_WithLastRole_ShouldReturnNull()
    {
        var existing = CreateEffectiveAccess();

        var result = _engine.RemoveRoleFromEffectiveAccess(existing, "ra-test-001");

        result.Should().BeNull();
    }

    [Fact]
    public void UpdateRoleDefinition_ShouldUpdateAffectedRoles()
    {
        var existing = CreateEffectiveAccess();
        var updatedDefinition = new RoleDefinition
        {
            Id = "rd-test-001",
            Name = "Updated Role Name",
            Permissions = new List<Permission>
            {
                new() { Actions = new List<string> { "Microsoft.Network/*" } }
            },
            Version = new VersionInfo { SequenceNumber = 2, ChangeId = "chg-def-002" }
        };

        var result = _engine.UpdateRoleDefinition(existing, updatedDefinition);

        result.EffectiveRoles[0].RoleName.Should().Be("Updated Role Name");
        result.EffectiveRoles[0].Permissions.Actions.Should().Contain("Microsoft.Network/*");
        result.EffectiveRoles[0].SourceVersions.RoleDefSequence.Should().Be(2);
    }

    private RoleAssignment CreateRoleAssignment(string id = "ra-test-001")
    {
        return new RoleAssignment
        {
            Id = id,
            PrincipalId = "user-001",
            Scope = "/subscriptions/sub-001",
            RoleDefinitionId = "rd-test-001",
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ChangeId = "chg-001"
            }
        };
    }

    private RoleDefinition CreateRoleDefinition()
    {
        return new RoleDefinition
        {
            Id = "rd-test-001",
            Name = "Test Role",
            Permissions = new List<Permission>
            {
                new() { Actions = new List<string> { "Microsoft.Storage/*" } }
            },
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                ChangeId = "chg-def-001"
            }
        };
    }

    private EffectiveAccess CreateEffectiveAccess()
    {
        return new EffectiveAccess
        {
            Id = "ea-user-001-hash",
            PrincipalId = "user-001",
            Scope = "/subscriptions/sub-001",
            EffectiveRoles = new List<EffectiveRole>
            {
                new()
                {
                    RoleDefinitionId = "rd-test-001",
                    RoleName = "Test Role",
                    AssignmentId = "ra-test-001",
                    AssignmentScope = "/subscriptions/sub-001",
                    Permissions = new Permission
                    {
                        Actions = new List<string> { "Microsoft.Storage/*" }
                    },
                    SourceVersions = new SourceVersions
                    {
                        AssignmentSequence = 1,
                        AssignmentChangeId = "chg-001"
                    }
                }
            },
            Sync = new SyncMetadata
            {
                DocumentVersion = 1,
                LastSyncedAt = DateTimeOffset.UtcNow
            },
            Watermarks = new Watermarks()
        };
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rbac.IntegrationTests.Fixtures;
using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.DataPlane;
using Rbac.Shared.Models.Requests;
using Rbac.Shared.Models.Responses;
using Rbac.Shared.Utilities;
using Rbac.SyncService.Processors;
using System.Net.Http.Json;

namespace Rbac.IntegrationTests.Scenarios;

/// <summary>
/// End-to-end integration tests for the RBAC system.
/// These tests require CosmosDB Emulator to be running.
/// </summary>
[Collection("CosmosDb")]
public class EndToEndRbacTests : IClassFixture<CosmosDbFixture>
{
    private readonly CosmosDbFixture _fixture;
    private readonly DenormalizationEngine _engine;

    public EndToEndRbacTests(CosmosDbFixture fixture)
    {
        _fixture = fixture;
        _engine = new DenormalizationEngine();
    }

    [Fact(Skip = "Requires CosmosDB Emulator")]
    public async Task FullRbacFlow_CreateRoleAndAssignment_ThenCheckAccess()
    {
        // Arrange
        var controlPlaneClient = new CosmosDbClient(_fixture.ControlPlaneSettings);
        var dataPlaneClient = new CosmosDbClient(_fixture.DataPlaneSettings);

        var roleDefinitionRepo = new CosmosDbRepository<RoleDefinition>(
            controlPlaneClient, ContainerNames.RoleDefinitions);
        var roleAssignmentRepo = new CosmosDbRepository<RoleAssignment>(
            controlPlaneClient, ContainerNames.RoleAssignments);
        var effectiveAccessRepo = new CosmosDbRepository<EffectiveAccess>(
            dataPlaneClient, ContainerNames.EffectiveAccess);

        // 1. Create Role Definition
        var roleDefinition = new RoleDefinition
        {
            Id = ScopeHasher.GenerateRoleDefinitionId(),
            PartitionKey = "RoleDefinition",
            Name = "Storage Reader",
            Description = "Can read storage accounts",
            Permissions = new List<Permission>
            {
                new()
                {
                    Actions = new List<string> { "Microsoft.Storage/storageAccounts/read" }
                }
            },
            AssignableScopes = new List<string> { "/" },
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ChangeId = ChangeIdGenerator.Generate()
            },
            Lifecycle = new LifecycleInfo { State = "Active", IsDeleted = false }
        };

        await roleDefinitionRepo.CreateAsync(roleDefinition, "RoleDefinition");

        // 2. Create Role Assignment
        var scope = "/subscriptions/sub-001";
        var roleAssignment = new RoleAssignment
        {
            Id = ScopeHasher.GenerateRoleAssignmentId(),
            Scope = scope,
            PrincipalId = "user-001",
            PrincipalType = "User",
            RoleDefinitionId = roleDefinition.Id,
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ChangeId = ChangeIdGenerator.Generate()
            },
            Lifecycle = new LifecycleInfo { State = "Active", IsDeleted = false }
        };

        await roleAssignmentRepo.CreateAsync(roleAssignment, scope);

        // 3. Simulate Sync Service: Create EffectiveAccess
        var effectiveAccess = _engine.CreateEffectiveAccess(roleAssignment, roleDefinition);
        await effectiveAccessRepo.CreateAsync(effectiveAccess, effectiveAccess.PrincipalId);

        // 4. Verify EffectiveAccess was created
        var retrieved = await effectiveAccessRepo.GetByIdAsync(effectiveAccess.Id, effectiveAccess.PrincipalId);

        retrieved.Should().NotBeNull();
        retrieved!.PrincipalId.Should().Be("user-001");
        retrieved.Scope.Should().Be(scope);
        retrieved.EffectiveRoles.Should().HaveCount(1);
        retrieved.EffectiveRoles[0].RoleDefinitionId.Should().Be(roleDefinition.Id);
        retrieved.EffectiveRoles[0].Permissions.Actions.Should().Contain("Microsoft.Storage/storageAccounts/read");
    }

    [Fact(Skip = "Requires CosmosDB Emulator")]
    public async Task ScopeInheritance_ParentScopeAssignment_ShouldBeVisibleAtChildScope()
    {
        // Arrange
        var controlPlaneClient = new CosmosDbClient(_fixture.ControlPlaneSettings);
        var dataPlaneClient = new CosmosDbClient(_fixture.DataPlaneSettings);

        var roleDefinitionRepo = new CosmosDbRepository<RoleDefinition>(
            controlPlaneClient, ContainerNames.RoleDefinitions);
        var roleAssignmentRepo = new CosmosDbRepository<RoleAssignment>(
            controlPlaneClient, ContainerNames.RoleAssignments);
        var effectiveAccessRepo = new CosmosDbRepository<EffectiveAccess>(
            dataPlaneClient, ContainerNames.EffectiveAccess);

        // Create role at subscription level
        var roleDefinition = new RoleDefinition
        {
            Id = ScopeHasher.GenerateRoleDefinitionId(),
            PartitionKey = "RoleDefinition",
            Name = "Contributor",
            Permissions = new List<Permission>
            {
                new() { Actions = new List<string> { "*" } }
            },
            AssignableScopes = new List<string> { "/" },
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ChangeId = ChangeIdGenerator.Generate()
            }
        };

        await roleDefinitionRepo.CreateAsync(roleDefinition, "RoleDefinition");

        // Assign at subscription level
        var subscriptionScope = "/subscriptions/sub-001";
        var roleAssignment = new RoleAssignment
        {
            Id = ScopeHasher.GenerateRoleAssignmentId(),
            Scope = subscriptionScope,
            PrincipalId = "user-002",
            PrincipalType = "User",
            RoleDefinitionId = roleDefinition.Id,
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ChangeId = ChangeIdGenerator.Generate()
            }
        };

        await roleAssignmentRepo.CreateAsync(roleAssignment, subscriptionScope);

        // Create EffectiveAccess at subscription level
        var effectiveAccess = _engine.CreateEffectiveAccess(roleAssignment, roleDefinition);
        await effectiveAccessRepo.CreateAsync(effectiveAccess, effectiveAccess.PrincipalId);

        // Verify scope hierarchy works
        var childScope = "/subscriptions/sub-001/resourceGroups/rg-001";
        var scopeHierarchy = ScopeParser.GetScopeHierarchy(childScope);

        // The hierarchy should include the subscription scope
        scopeHierarchy.Should().Contain(subscriptionScope);

        // When checking access at child scope, we should find the parent assignment
        EffectiveAccess? foundAccess = null;
        foreach (var scope in scopeHierarchy)
        {
            var id = ScopeHasher.GenerateEffectiveAccessId("user-002", scope);
            var access = await effectiveAccessRepo.GetByIdAsync(id, "user-002");
            if (access != null)
            {
                foundAccess = access;
                break;
            }
        }

        foundAccess.Should().NotBeNull();
        foundAccess!.EffectiveRoles.Should().HaveCount(1);
    }
}

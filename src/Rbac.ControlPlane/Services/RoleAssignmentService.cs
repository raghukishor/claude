using Rbac.ControlPlane.Repositories;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.Requests;
using Rbac.Shared.Utilities;

namespace Rbac.ControlPlane.Services;

public class RoleAssignmentService : IRoleAssignmentService
{
    private readonly IRoleAssignmentRepository _roleAssignmentRepository;
    private readonly IRoleDefinitionRepository _roleDefinitionRepository;

    public RoleAssignmentService(
        IRoleAssignmentRepository roleAssignmentRepository,
        IRoleDefinitionRepository roleDefinitionRepository)
    {
        _roleAssignmentRepository = roleAssignmentRepository;
        _roleDefinitionRepository = roleDefinitionRepository;
    }

    public async Task<RoleAssignment?> GetByIdAsync(string id, string scope)
    {
        return await _roleAssignmentRepository.GetByIdAsync(id, scope);
    }

    public async Task<List<RoleAssignment>> ListAsync(string scope)
    {
        return await _roleAssignmentRepository.ListAsync(scope);
    }

    public async Task<List<RoleAssignment>> ListByPrincipalAsync(string principalId)
    {
        return await _roleAssignmentRepository.ListByPrincipalAsync(principalId);
    }

    public async Task<RoleAssignment> CreateAsync(string scope, CreateRoleAssignmentRequest request)
    {
        await ValidateRequestAsync(scope, request);

        var roleAssignment = new RoleAssignment
        {
            Id = ScopeHasher.GenerateRoleAssignmentId(),
            Scope = scope,
            PrincipalId = request.PrincipalId,
            PrincipalType = request.PrincipalType,
            RoleDefinitionId = request.RoleDefinitionId,
            Condition = request.Condition != null ? new Condition
            {
                Expression = request.Condition.Expression,
                Version = request.Condition.Version ?? "1.0"
            } : null,
            Version = new VersionInfo
            {
                SequenceNumber = 1,
                Timestamp = DateTimeOffset.UtcNow,
                ChangeId = ChangeIdGenerator.Generate()
            },
            Lifecycle = new LifecycleInfo
            {
                State = "Active",
                IsDeleted = false
            },
            Metadata = new MetadataInfo
            {
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "system"
            },
            References = new ReferenceInfo
            {
                RoleDefinitionVersion = 1 // Will be updated with actual version
            }
        };

        return await _roleAssignmentRepository.CreateAsync(roleAssignment);
    }

    public async Task DeleteAsync(string id, string scope)
    {
        await _roleAssignmentRepository.DeleteAsync(id, scope);
    }

    private async Task ValidateRequestAsync(string scope, CreateRoleAssignmentRequest request)
    {
        if (!ScopeParser.IsValidScope(scope))
            throw new ArgumentException($"Invalid scope: {scope}");

        if (string.IsNullOrWhiteSpace(request.PrincipalId))
            throw new ArgumentException("PrincipalId is required");

        if (string.IsNullOrWhiteSpace(request.RoleDefinitionId))
            throw new ArgumentException("RoleDefinitionId is required");

        // Validate role definition exists
        var roleDefinition = await _roleDefinitionRepository.GetByIdAsync(request.RoleDefinitionId);
        if (roleDefinition == null)
            throw new KeyNotFoundException($"Role definition {request.RoleDefinitionId} not found");

        // Validate scope is within assignable scopes
        var isValidScope = roleDefinition.AssignableScopes.Any(assignableScope =>
            ScopeParser.IsChildScope(assignableScope, scope));

        if (!isValidScope)
            throw new ArgumentException($"Scope {scope} is not within the assignable scopes of role definition {request.RoleDefinitionId}");
    }
}

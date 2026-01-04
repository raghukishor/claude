using Rbac.ControlPlane.Repositories;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.Requests;
using Rbac.Shared.Utilities;

namespace Rbac.ControlPlane.Services;

public class RoleDefinitionService : IRoleDefinitionService
{
    private readonly IRoleDefinitionRepository _repository;

    public RoleDefinitionService(IRoleDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<RoleDefinition?> GetByIdAsync(string id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<List<RoleDefinition>> ListAsync(string? scope = null)
    {
        return await _repository.ListAsync(scope);
    }

    public async Task<RoleDefinition> CreateAsync(CreateRoleDefinitionRequest request)
    {
        ValidateRequest(request);

        var roleDefinition = new RoleDefinition
        {
            Id = ScopeHasher.GenerateRoleDefinitionId(),
            Name = request.Name,
            Description = request.Description,
            Permissions = request.Permissions.Select(p => new Permission
            {
                Actions = p.Actions,
                NotActions = p.NotActions,
                DataActions = p.DataActions,
                NotDataActions = p.NotDataActions
            }).ToList(),
            AssignableScopes = request.AssignableScopes,
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
                CreatedBy = "system" // Would be set by auth in real implementation
            }
        };

        return await _repository.CreateAsync(roleDefinition);
    }

    public async Task<RoleDefinition> UpdateAsync(string id, UpdateRoleDefinitionRequest request, string? etag = null)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            throw new KeyNotFoundException($"Role definition {id} not found");

        if (request.Name != null)
            existing.Name = request.Name;

        if (request.Description != null)
            existing.Description = request.Description;

        if (request.Permissions != null)
        {
            existing.Permissions = request.Permissions.Select(p => new Permission
            {
                Actions = p.Actions,
                NotActions = p.NotActions,
                DataActions = p.DataActions,
                NotDataActions = p.NotDataActions
            }).ToList();
        }

        if (request.AssignableScopes != null)
        {
            foreach (var scope in request.AssignableScopes)
            {
                if (!ScopeParser.IsValidScope(scope))
                    throw new ArgumentException($"Invalid scope: {scope}");
            }
            existing.AssignableScopes = request.AssignableScopes;
        }

        // Update version info
        existing.Version ??= new VersionInfo();
        existing.Version.SequenceNumber++;
        existing.Version.Timestamp = DateTimeOffset.UtcNow;
        existing.Version.ChangeId = ChangeIdGenerator.Generate();

        // Update metadata
        existing.Metadata ??= new MetadataInfo();
        existing.Metadata.UpdatedAt = DateTimeOffset.UtcNow;
        existing.Metadata.UpdatedBy = "system";

        return await _repository.UpdateAsync(existing, etag);
    }

    public async Task DeleteAsync(string id)
    {
        await _repository.DeleteAsync(id);
    }

    private void ValidateRequest(CreateRoleDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required");

        if (request.Permissions == null || request.Permissions.Count == 0)
            throw new ArgumentException("At least one permission is required");

        if (request.AssignableScopes == null || request.AssignableScopes.Count == 0)
            throw new ArgumentException("At least one assignable scope is required");

        foreach (var scope in request.AssignableScopes)
        {
            if (!ScopeParser.IsValidScope(scope))
                throw new ArgumentException($"Invalid scope: {scope}");
        }

        foreach (var permission in request.Permissions)
        {
            foreach (var action in permission.Actions ?? new List<string>())
            {
                if (!WildcardMatcher.IsValidPattern(action))
                    throw new ArgumentException($"Invalid action pattern: {action}");
            }
        }
    }
}

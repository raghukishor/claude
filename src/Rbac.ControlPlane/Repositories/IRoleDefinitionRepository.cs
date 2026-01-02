using Rbac.Shared.Models.ControlPlane;

namespace Rbac.ControlPlane.Repositories;

public interface IRoleDefinitionRepository
{
    Task<RoleDefinition?> GetByIdAsync(string id);
    Task<List<RoleDefinition>> ListAsync(string? scope = null, bool includeDeleted = false);
    Task<RoleDefinition> CreateAsync(RoleDefinition roleDefinition);
    Task<RoleDefinition> UpdateAsync(RoleDefinition roleDefinition, string? etag = null);
    Task DeleteAsync(string id);
}

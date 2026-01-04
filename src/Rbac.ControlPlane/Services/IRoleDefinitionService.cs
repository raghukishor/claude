using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.Requests;

namespace Rbac.ControlPlane.Services;

public interface IRoleDefinitionService
{
    Task<RoleDefinition?> GetByIdAsync(string id);
    Task<List<RoleDefinition>> ListAsync(string? scope = null);
    Task<RoleDefinition> CreateAsync(CreateRoleDefinitionRequest request);
    Task<RoleDefinition> UpdateAsync(string id, UpdateRoleDefinitionRequest request, string? etag = null);
    Task DeleteAsync(string id);
}

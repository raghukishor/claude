using Rbac.Shared.Models.ControlPlane;

namespace Rbac.ControlPlane.Repositories;

public interface IRoleAssignmentRepository
{
    Task<RoleAssignment?> GetByIdAsync(string id, string scope);
    Task<List<RoleAssignment>> ListAsync(string scope, bool includeDeleted = false);
    Task<List<RoleAssignment>> ListByPrincipalAsync(string principalId, bool includeDeleted = false);
    Task<RoleAssignment> CreateAsync(RoleAssignment roleAssignment);
    Task<RoleAssignment> UpdateAsync(RoleAssignment roleAssignment, string? etag = null);
    Task DeleteAsync(string id, string scope);
}

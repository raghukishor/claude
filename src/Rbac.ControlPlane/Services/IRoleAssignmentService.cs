using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.Requests;

namespace Rbac.ControlPlane.Services;

public interface IRoleAssignmentService
{
    Task<RoleAssignment?> GetByIdAsync(string id, string scope);
    Task<List<RoleAssignment>> ListAsync(string scope);
    Task<List<RoleAssignment>> ListByPrincipalAsync(string principalId);
    Task<RoleAssignment> CreateAsync(string scope, CreateRoleAssignmentRequest request);
    Task DeleteAsync(string id, string scope);
}

using Rbac.Shared.Models.ControlPlane;

namespace Rbac.DataPlane.Repositories;

public interface IRoleAssignmentRepository
{
    Task<List<RoleAssignment>> ListByPrincipalAsync(string principalId);
}

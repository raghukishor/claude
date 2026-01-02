using Rbac.Shared.Models.ControlPlane;

namespace Rbac.DataPlane.Repositories;

public interface IRoleDefinitionRepository
{
    Task<RoleDefinition?> GetByIdAsync(string id);
}
